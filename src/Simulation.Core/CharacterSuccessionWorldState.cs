using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core;

public sealed class CharacterSuccessionWorldState
    : IAuthoritativeCharacterSuccessionWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, HeirDesignationState> designations = [];
    private readonly SortedDictionary<EntityId, EntityId> activeByDesignator = [];
    private readonly SortedDictionary<EntityId, HeirDesignationHistoryAggregate> history = [];
    private readonly SortedDictionary<EntityId, SuccessionClaimState> claims = [];
    private readonly Dictionary<(EntityId Subject, EntityId Claimant), EntityId>
        activeClaimByPair = [];
    private readonly SortedDictionary<EntityId, SuccessionClaimHistoryAggregate> claimHistory = [];
    private readonly SortedDictionary<EntityId, SuccessionSupportState> supports = [];
    private readonly Dictionary<(EntityId Subject, EntityId Supporter), EntityId>
        activeSupportByPair = [];
    private readonly SortedDictionary<EntityId, SuccessionSupportHistoryAggregate>
        supportHistory = [];
    private CampaignCalendar calendar;

    public CharacterSuccessionWorldState(
        CharacterSuccessionWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException(
                "Character-succession snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException(
                "Authoritative character query cannot be null.");
        ValidateCalendar(calendar);
        this.calendar = calendar;
        ValidateSnapshotShape(snapshot);
        AddDesignations(snapshot.Designations);
        AddHistory(snapshot.History);
        AddClaims(snapshot.Claims);
        AddClaimHistory(snapshot.ClaimHistory);
        AddSupports(snapshot.Supports);
        AddSupportHistory(snapshot.SupportHistory);
        ValidateRetentionBounds();
    }

    public IReadOnlyList<HeirDesignationState> Designations =>
        designations.Values.Select(Clone).ToArray();

    public IReadOnlyList<HeirDesignationHistoryAggregate> History =>
        history.Values.Select(Clone).ToArray();

    public bool TryGetCurrentDesignation(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationState? designation)
    {
        RequireCharacter(designatorCharacterId, "Heir-designation query designator");
        if (activeByDesignator.TryGetValue(
                designatorCharacterId,
                out EntityId designationId))
        {
            designation = Clone(designations[designationId]);
            return true;
        }

        designation = null;
        return false;
    }

    public IReadOnlyList<HeirDesignationState> GetDesignationRecordsInvolving(
        EntityId characterId)
    {
        RequireCharacter(characterId, "Heir-designation query character");
        return designations.Values
            .Where(item => item.DesignatorCharacterId == characterId
                || item.HeirCharacterId == characterId)
            .Select(Clone)
            .ToArray();
    }

    public bool TryGetHistory(
        EntityId designatorCharacterId,
        [NotNullWhen(true)] out HeirDesignationHistoryAggregate? aggregate)
    {
        RequireCharacter(designatorCharacterId, "Heir-designation history query designator");
        if (history.TryGetValue(
                designatorCharacterId,
                out HeirDesignationHistoryAggregate? stored))
        {
            aggregate = Clone(stored);
            return true;
        }

        aggregate = null;
        return false;
    }

    public bool TryGetActiveClaim(
        EntityId subjectCharacterId,
        EntityId claimantCharacterId,
        [NotNullWhen(true)] out SuccessionClaimState? claim)
    {
        RequireCharacter(subjectCharacterId, "Succession-claim query subject");
        RequireCharacter(claimantCharacterId, "Succession-claim query claimant");
        if (activeClaimByPair.TryGetValue(
                (subjectCharacterId, claimantCharacterId),
                out EntityId claimId))
        {
            claim = Clone(claims[claimId]);
            return true;
        }

        claim = null;
        return false;
    }

    public IReadOnlyList<SuccessionClaimState> GetActiveClaimsForSubject(
        EntityId subjectCharacterId)
    {
        RequireCharacter(subjectCharacterId, "Succession-claim query subject");
        return claims.Values
            .Where(item => item.SubjectCharacterId == subjectCharacterId
                && item.Status == SuccessionClaimStatus.Active)
            .OrderBy(item => item.ClaimId)
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<SuccessionClaimState> GetRecentClaimRecordsForSubject(
        EntityId subjectCharacterId)
    {
        RequireCharacter(subjectCharacterId, "Succession-claim query subject");
        return claims.Values
            .Where(item => item.SubjectCharacterId == subjectCharacterId)
            .OrderBy(item => item.ClaimId)
            .Select(Clone)
            .ToArray();
    }

    public bool TryGetClaimHistory(
        EntityId subjectCharacterId,
        [NotNullWhen(true)] out SuccessionClaimHistoryAggregate? aggregate)
    {
        RequireCharacter(subjectCharacterId, "Succession-claim history query subject");
        if (claimHistory.TryGetValue(
                subjectCharacterId,
                out SuccessionClaimHistoryAggregate? stored))
        {
            aggregate = Clone(stored);
            return true;
        }

        aggregate = null;
        return false;
    }

    public bool TryGetCurrentSupport(
        EntityId subjectId,
        EntityId supporterId,
        [NotNullWhen(true)] out SuccessionSupportState? support)
    {
        RequireCharacter(subjectId, "Succession-support query subject");
        RequireCharacter(supporterId, "Succession-support query supporter");
        if (activeSupportByPair.TryGetValue(
                (subjectId, supporterId),
                out EntityId supportId))
        {
            support = Clone(supports[supportId]);
            return true;
        }

        support = null;
        return false;
    }

    public IReadOnlyList<SuccessionSupportState> GetActiveSupportsForSubject(
        EntityId subjectId)
    {
        RequireCharacter(subjectId, "Succession-support query subject");
        return supports.Values
            .Where(item => item.SubjectId == subjectId
                && item.Status == SuccessionSupportStatus.Active)
            .OrderBy(item => item.SupportId)
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<SuccessionSupportState> GetActiveSupportsForCandidate(
        EntityId subjectId,
        EntityId supportedCandidateId)
    {
        RequireCharacter(subjectId, "Succession-support query subject");
        RequireCharacter(
            supportedCandidateId,
            "Succession-support query supported candidate");
        return supports.Values
            .Where(item => item.SubjectId == subjectId
                && item.SupportedCandidateId == supportedCandidateId
                && item.Status == SuccessionSupportStatus.Active)
            .OrderBy(item => item.SupportId)
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<SuccessionSupportState> GetRecentSupportRecordsForSubject(
        EntityId subjectId)
    {
        RequireCharacter(subjectId, "Succession-support query subject");
        return supports.Values
            .Where(item => item.SubjectId == subjectId)
            .OrderBy(item => item.SupportId)
            .Select(Clone)
            .ToArray();
    }

    public bool TryGetSupportHistory(
        EntityId subjectId,
        [NotNullWhen(true)] out SuccessionSupportHistoryAggregate? aggregate)
    {
        RequireCharacter(subjectId, "Succession-support history query subject");
        if (supportHistory.TryGetValue(
                subjectId,
                out SuccessionSupportHistoryAggregate? stored))
        {
            aggregate = Clone(stored);
            return true;
        }

        aggregate = null;
        return false;
    }

    public SuccessionCandidateEvaluationResult EvaluateCandidate(
        SuccessionCandidateEvaluationRequest request)
    {
        EntityId subjectCharacterId = request?.SubjectCharacterId ?? default;
        EntityId candidateCharacterId = request?.CandidateCharacterId ?? default;
        List<SuccessionCandidateEligibilityReason> issues = [];
        List<SuccessionCandidateBasisEvidence> recognizedBases = [];

        bool requestIsValid = request is not null
            && request.ContractVersion
                == CharacterSuccessionContractVersions.CandidateEvaluation;
        if (!requestIsValid)
        {
            AddIssue(issues, SuccessionCandidateEligibilityReason.InvalidRequest);
        }

        SuccessionCandidateEligibilityRule? rule = request?.Rule;
        HashSet<SuccessionCandidateBasis> allowedBases = [];
        HashSet<CharacterCustodyStatus> allowedCustodyStatuses = [];
        bool ruleIsValid = ValidateEligibilityRule(
            rule,
            allowedBases,
            allowedCustodyStatuses,
            issues);

        AuthoritativeCharacterProfile? subject = GetEvaluationCharacter(
            subjectCharacterId,
            isSubject: true,
            issues);
        AuthoritativeCharacterProfile? candidate = GetEvaluationCharacter(
            candidateCharacterId,
            isSubject: false,
            issues);

        if (subject is not null
            && candidate is not null
            && subject.CharacterId == candidate.CharacterId)
        {
            AddIssue(issues, SuccessionCandidateEligibilityReason.SameCharacter);
        }

        if (candidate is not null && requestIsValid && ruleIsValid)
        {
            if (candidate.Condition.VitalStatus != CharacterVitalStatus.Alive)
            {
                AddIssue(issues, SuccessionCandidateEligibilityReason.CandidateDead);
            }

            if (candidate.BirthDate.CompareTo(calendar.Date) <= 0
                && CalculateAge(candidate.BirthDate, calendar.Date)
                    < rule!.MinimumCandidateAge)
            {
                AddIssue(
                    issues,
                    SuccessionCandidateEligibilityReason.CandidateBelowMinimumAge);
            }

            if (candidate.Condition.IsIncapacitated
                && !rule!.AllowsIncapacitatedCandidates)
            {
                AddIssue(
                    issues,
                    SuccessionCandidateEligibilityReason.CandidateIncapacitated);
            }

            if (!allowedCustodyStatuses.Contains(candidate.Condition.CustodyStatus))
            {
                AddIssue(
                    issues,
                    SuccessionCandidateEligibilityReason.CandidateCustodyNotAllowed);
            }
        }

        if (subject is not null
            && candidate is not null
            && subject.CharacterId != candidate.CharacterId
            && subject.BirthDate.CompareTo(calendar.Date) <= 0
            && candidate.BirthDate.CompareTo(calendar.Date) <= 0
            && requestIsValid
            && ruleIsValid)
        {
            recognizedBases.AddRange(FindRecognizedBases(
                subject.CharacterId,
                candidate.CharacterId,
                rule!,
                allowedBases));
            if (recognizedBases.Count == 0)
            {
                AddIssue(issues, SuccessionCandidateEligibilityReason.NoRecognizedBasis);
            }
        }

        SuccessionCandidateBasisEvidence[] canonicalBases = recognizedBases
            .OrderBy(item => item.Basis)
            .ThenBy(item => item.DescendantGeneration)
            .ThenBy(item => item.SourceDesignationId)
            .Select(Clone)
            .ToArray();
        SuccessionCandidateEligibilityIssue[] canonicalIssues = issues
            .Distinct()
            .Order()
            .Select(item => new SuccessionCandidateEligibilityIssue(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                item))
            .ToArray();
        return new SuccessionCandidateEvaluationResult(
            CharacterSuccessionContractVersions.CandidateEvaluation,
            subjectCharacterId.IsValid ? subjectCharacterId : null,
            candidateCharacterId.IsValid ? candidateCharacterId : null,
            calendar.Date,
            calendar.TurnIndex,
            canonicalBases,
            canonicalIssues,
            canonicalIssues.Length == 0 && canonicalBases.Length > 0);
    }

    public SuccessionCandidateSetResult FindEligibleCandidates(
        SuccessionCandidateSetRequest request)
    {
        EntityId subjectCharacterId = request?.SubjectCharacterId ?? default;
        int maximumCandidates = request?.MaximumCandidates ?? 0;
        List<SuccessionCandidateSetIssueReason> issues = [];
        List<SuccessionCandidateEligibilityReason> eligibilityIssues = [];

        bool requestIsValid = request is not null
            && request.ContractVersion == CharacterSuccessionContractVersions.CandidateSet;
        if (!requestIsValid)
        {
            AddIssue(issues, SuccessionCandidateSetIssueReason.InvalidRequest);
        }

        bool maximumIsValid = maximumCandidates > 0;
        if (!maximumIsValid)
        {
            AddIssue(issues, SuccessionCandidateSetIssueReason.InvalidMaximumCandidates);
        }

        HashSet<SuccessionCandidateBasis> allowedBases = [];
        HashSet<CharacterCustodyStatus> allowedCustodyStatuses = [];
        bool ruleIsValid = ValidateEligibilityRule(
            request?.Rule,
            allowedBases,
            allowedCustodyStatuses,
            eligibilityIssues);
        AuthoritativeCharacterProfile? subject = GetEvaluationCharacter(
            subjectCharacterId,
            isSubject: true,
            eligibilityIssues);
        foreach (SuccessionCandidateEligibilityReason issue in eligibilityIssues)
        {
            AddIssue(issues, ToCandidateSetIssue(issue));
        }

        bool subjectIsValid = subject is not null
            && subject.BirthDate.CompareTo(calendar.Date) <= 0;
        if (!requestIsValid || !maximumIsValid || !ruleIsValid || !subjectIsValid)
        {
            return CreateCandidateSetResult(
                subjectCharacterId,
                maximumCandidates,
                eligibleCandidateCount: 0,
                [],
                issues,
                SuccessionCandidateSetStatus.InvalidRequest);
        }

        SuccessionCandidateEligibilityRule frozenRule = new(
            CharacterSuccessionContractVersions.CandidateEligibilityRule,
            allowedBases.Order().ToArray(),
            request!.Rule.MaximumDescendantGeneration,
            request.Rule.MinimumCandidateAge,
            request.Rule.AllowsIncapacitatedCandidates,
            allowedCustodyStatuses.Order().ToArray());
        List<SuccessionCandidateSetEntry> eligibleCandidates = [];
        int eligibleCandidateCount = 0;
        foreach (AuthoritativeCharacterProfile candidate in characters.Profiles)
        {
            SuccessionCandidateEvaluationResult evaluation = EvaluateCandidate(new(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                subjectCharacterId,
                candidate.CharacterId,
                frozenRule));
            if (!evaluation.IsEligible)
            {
                continue;
            }

            eligibleCandidateCount = checked(eligibleCandidateCount + 1);
            if (eligibleCandidateCount > maximumCandidates)
            {
                eligibleCandidates.Clear();
                continue;
            }

            eligibleCandidates.Add(new(
                CharacterSuccessionContractVersions.CandidateSet,
                candidate.CharacterId,
                evaluation.RecognizedBases.Select(Clone).ToArray()));
        }

        if (eligibleCandidateCount > maximumCandidates)
        {
            AddIssue(issues, SuccessionCandidateSetIssueReason.MaximumCandidatesExceeded);
            return CreateCandidateSetResult(
                subjectCharacterId,
                maximumCandidates,
                eligibleCandidateCount,
                [],
                issues,
                SuccessionCandidateSetStatus.MaximumCandidatesExceeded);
        }

        SuccessionCandidateSetEntry[] canonicalCandidates = eligibleCandidates
            .OrderBy(item => item.CandidateCharacterId)
            .Select(Clone)
            .ToArray();
        return CreateCandidateSetResult(
            subjectCharacterId,
            maximumCandidates,
            eligibleCandidateCount,
            canonicalCandidates,
            issues,
            SuccessionCandidateSetStatus.Complete);
    }

    public CharacterSuccessionWorldSnapshot CaptureSnapshot() => new(
        CharacterSuccessionContractVersions.Snapshot,
        designations.Values.Select(Clone).ToArray(),
        history.Values.Select(Clone).ToArray(),
        claims.Values.Select(Clone).ToArray(),
        claimHistory.Values.Select(Clone).ToArray(),
        supports.Values.Select(Clone).ToArray(),
        supportHistory.Values.Select(Clone).ToArray());

    private bool ValidateEligibilityRule(
        SuccessionCandidateEligibilityRule? rule,
        ISet<SuccessionCandidateBasis> allowedBases,
        ISet<CharacterCustodyStatus> allowedCustodyStatuses,
        ICollection<SuccessionCandidateEligibilityReason> issues)
    {
        if (rule is null)
        {
            AddIssue(issues, SuccessionCandidateEligibilityReason.UnsupportedRuleVersion);
            return false;
        }

        if (rule.ContractVersion
            != CharacterSuccessionContractVersions.CandidateEligibilityRule)
        {
            AddIssue(issues, SuccessionCandidateEligibilityReason.UnsupportedRuleVersion);
        }

        if (rule.AllowedBases is not { Count: > 0 })
        {
            AddIssue(issues, SuccessionCandidateEligibilityReason.MissingAllowedBasis);
        }
        else
        {
            foreach (SuccessionCandidateBasis basis in rule.AllowedBases)
            {
                if (!Enum.IsDefined(basis))
                {
                    AddIssue(
                        issues,
                        SuccessionCandidateEligibilityReason.UnsupportedAllowedBasis);
                }
                else if (!allowedBases.Add(basis))
                {
                    AddIssue(
                        issues,
                        SuccessionCandidateEligibilityReason.DuplicateAllowedBasis);
                }
            }
        }

        if (rule.MaximumDescendantGeneration is < 1
            or > CharacterSuccessionLimits.MaximumEvaluatedDescendantGeneration)
        {
            AddIssue(
                issues,
                SuccessionCandidateEligibilityReason.InvalidMaximumDescendantGeneration);
        }

        if (rule.MinimumCandidateAge is < 0
            or > CharacterSuccessionLimits.MaximumConfiguredMinimumCandidateAge)
        {
            AddIssue(
                issues,
                SuccessionCandidateEligibilityReason.InvalidMinimumCandidateAge);
        }

        if (rule.AllowedCustodyStatuses is not { Count: > 0 })
        {
            AddIssue(
                issues,
                SuccessionCandidateEligibilityReason.MissingAllowedCustodyStatus);
        }
        else
        {
            foreach (CharacterCustodyStatus custodyStatus in rule.AllowedCustodyStatuses)
            {
                if (!Enum.IsDefined(custodyStatus))
                {
                    AddIssue(
                        issues,
                        SuccessionCandidateEligibilityReason.UnsupportedAllowedCustodyStatus);
                }
                else if (!allowedCustodyStatuses.Add(custodyStatus))
                {
                    AddIssue(
                        issues,
                        SuccessionCandidateEligibilityReason.DuplicateAllowedCustodyStatus);
                }
            }
        }

        return !issues.Any(IsRuleIssue);
    }

    private AuthoritativeCharacterProfile? GetEvaluationCharacter(
        EntityId characterId,
        bool isSubject,
        ICollection<SuccessionCandidateEligibilityReason> issues)
    {
        SuccessionCandidateEligibilityReason invalid = isSubject
            ? SuccessionCandidateEligibilityReason.InvalidSubject
            : SuccessionCandidateEligibilityReason.InvalidCandidate;
        SuccessionCandidateEligibilityReason unknown = isSubject
            ? SuccessionCandidateEligibilityReason.UnknownSubject
            : SuccessionCandidateEligibilityReason.UnknownCandidate;
        SuccessionCandidateEligibilityReason notBorn = isSubject
            ? SuccessionCandidateEligibilityReason.SubjectNotBorn
            : SuccessionCandidateEligibilityReason.CandidateNotBorn;
        if (!characterId.IsValid)
        {
            AddIssue(issues, invalid);
            return null;
        }

        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            AddIssue(issues, unknown);
            return null;
        }

        if (profile.BirthDate.CompareTo(calendar.Date) > 0)
        {
            AddIssue(issues, notBorn);
        }

        return profile;
    }

    private IReadOnlyList<SuccessionCandidateBasisEvidence> FindRecognizedBases(
        EntityId subjectCharacterId,
        EntityId candidateCharacterId,
        SuccessionCandidateEligibilityRule rule,
        IReadOnlySet<SuccessionCandidateBasis> allowedBases)
    {
        List<SuccessionCandidateBasisEvidence> result = [];
        if (allowedBases.Contains(SuccessionCandidateBasis.ActiveDesignation)
            && activeByDesignator.TryGetValue(
                subjectCharacterId,
                out EntityId designationId)
            && designations[designationId].HeirCharacterId == candidateCharacterId)
        {
            result.Add(new(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                SuccessionCandidateBasis.ActiveDesignation,
                null,
                designationId));
        }

        Dictionary<SuccessionCandidateBasis, int> descendantGenerations = [];
        Queue<(EntityId CharacterId, SuccessionCandidateBasis Basis, int Generation)> pending = [];
        HashSet<(EntityId CharacterId, SuccessionCandidateBasis Basis)> visited = [];
        if (!characters.TryGetCharacterProfile(
                subjectCharacterId,
                out AuthoritativeCharacterProfile? subject))
        {
            return result;
        }

        foreach (CharacterChildLink child in subject.ChildLinks)
        {
            SuccessionCandidateBasis basis = ToDescendantBasis(child.Kind);
            pending.Enqueue((child.ChildCharacterId, basis, 1));
        }

        while (pending.TryDequeue(out var item))
        {
            if (item.Generation > rule.MaximumDescendantGeneration
                || !visited.Add((item.CharacterId, item.Basis)))
            {
                continue;
            }

            if (item.CharacterId == candidateCharacterId
                && allowedBases.Contains(item.Basis)
                && (!descendantGenerations.TryGetValue(item.Basis, out int previous)
                    || item.Generation < previous))
            {
                descendantGenerations[item.Basis] = item.Generation;
            }

            if (item.Generation == rule.MaximumDescendantGeneration
                || !characters.TryGetCharacterProfile(
                    item.CharacterId,
                    out AuthoritativeCharacterProfile? descendant))
            {
                continue;
            }

            foreach (CharacterChildLink child in descendant.ChildLinks)
            {
                pending.Enqueue((
                    child.ChildCharacterId,
                    CombineDescendantBasis(item.Basis, child.Kind),
                    checked(item.Generation + 1)));
            }
        }

        result.AddRange(descendantGenerations.Select(item =>
            new SuccessionCandidateBasisEvidence(
                CharacterSuccessionContractVersions.CandidateEvaluation,
                item.Key,
                item.Value,
                null)));
        return result;
    }

    private static SuccessionCandidateBasis ToDescendantBasis(
        ParentChildLinkKind kind) => kind switch
        {
            ParentChildLinkKind.Biological => SuccessionCandidateBasis.BiologicalDescendant,
            ParentChildLinkKind.LegalAdoptive =>
                SuccessionCandidateBasis.LegalAdoptiveDescendant,
            ParentChildLinkKind.UnspecifiedLegacy =>
                SuccessionCandidateBasis.UnspecifiedLegacyDescendant,
            _ => throw new SimulationValidationException(
                $"Unsupported succession parent-child link kind '{kind}'."),
        };

    private static SuccessionCandidateBasis CombineDescendantBasis(
        SuccessionCandidateBasis existing,
        ParentChildLinkKind next) =>
        existing == SuccessionCandidateBasis.UnspecifiedLegacyDescendant
            || next == ParentChildLinkKind.UnspecifiedLegacy
                ? SuccessionCandidateBasis.UnspecifiedLegacyDescendant
                : existing == SuccessionCandidateBasis.LegalAdoptiveDescendant
                    || next == ParentChildLinkKind.LegalAdoptive
                        ? SuccessionCandidateBasis.LegalAdoptiveDescendant
                        : SuccessionCandidateBasis.BiologicalDescendant;

    private static int CalculateAge(CampaignDate birthDate, CampaignDate currentDate)
    {
        int age = currentDate.Year - birthDate.Year;
        if (currentDate.Month < birthDate.Month
            || (currentDate.Month == birthDate.Month && currentDate.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private static bool IsRuleIssue(SuccessionCandidateEligibilityReason reason) =>
        reason is >= SuccessionCandidateEligibilityReason.UnsupportedRuleVersion
            and <= SuccessionCandidateEligibilityReason.DuplicateAllowedCustodyStatus;

    private SuccessionCandidateSetResult CreateCandidateSetResult(
        EntityId subjectCharacterId,
        int maximumCandidates,
        int eligibleCandidateCount,
        IReadOnlyList<SuccessionCandidateSetEntry> candidates,
        IEnumerable<SuccessionCandidateSetIssueReason> issues,
        SuccessionCandidateSetStatus status) => new(
            CharacterSuccessionContractVersions.CandidateSet,
            subjectCharacterId.IsValid ? subjectCharacterId : null,
            calendar.Date,
            calendar.TurnIndex,
            maximumCandidates,
            eligibleCandidateCount,
            candidates.Select(Clone).ToArray(),
            issues
                .Distinct()
                .Order()
                .Select(item => new SuccessionCandidateSetIssue(
                    CharacterSuccessionContractVersions.CandidateSet,
                    item))
                .ToArray(),
            status);

    private static SuccessionCandidateSetIssueReason ToCandidateSetIssue(
        SuccessionCandidateEligibilityReason reason) => reason switch
        {
            SuccessionCandidateEligibilityReason.UnsupportedRuleVersion =>
                SuccessionCandidateSetIssueReason.UnsupportedRuleVersion,
            SuccessionCandidateEligibilityReason.MissingAllowedBasis =>
                SuccessionCandidateSetIssueReason.MissingAllowedBasis,
            SuccessionCandidateEligibilityReason.UnsupportedAllowedBasis =>
                SuccessionCandidateSetIssueReason.UnsupportedAllowedBasis,
            SuccessionCandidateEligibilityReason.DuplicateAllowedBasis =>
                SuccessionCandidateSetIssueReason.DuplicateAllowedBasis,
            SuccessionCandidateEligibilityReason.InvalidMaximumDescendantGeneration =>
                SuccessionCandidateSetIssueReason.InvalidMaximumDescendantGeneration,
            SuccessionCandidateEligibilityReason.InvalidMinimumCandidateAge =>
                SuccessionCandidateSetIssueReason.InvalidMinimumCandidateAge,
            SuccessionCandidateEligibilityReason.MissingAllowedCustodyStatus =>
                SuccessionCandidateSetIssueReason.MissingAllowedCustodyStatus,
            SuccessionCandidateEligibilityReason.UnsupportedAllowedCustodyStatus =>
                SuccessionCandidateSetIssueReason.UnsupportedAllowedCustodyStatus,
            SuccessionCandidateEligibilityReason.DuplicateAllowedCustodyStatus =>
                SuccessionCandidateSetIssueReason.DuplicateAllowedCustodyStatus,
            SuccessionCandidateEligibilityReason.InvalidSubject =>
                SuccessionCandidateSetIssueReason.InvalidSubject,
            SuccessionCandidateEligibilityReason.UnknownSubject =>
                SuccessionCandidateSetIssueReason.UnknownSubject,
            SuccessionCandidateEligibilityReason.SubjectNotBorn =>
                SuccessionCandidateSetIssueReason.SubjectNotBorn,
            _ => throw new SimulationValidationException(
                $"Unsupported candidate-set issue mapping '{reason}'."),
        };

    private static void AddIssue(
        ICollection<SuccessionCandidateEligibilityReason> issues,
        SuccessionCandidateEligibilityReason issue)
    {
        if (!issues.Contains(issue))
        {
            issues.Add(issue);
        }
    }

    private static void AddIssue(
        ICollection<SuccessionCandidateSetIssueReason> issues,
        SuccessionCandidateSetIssueReason issue)
    {
        if (!issues.Contains(issue))
        {
            issues.Add(issue);
        }
    }

    private static SuccessionCandidateBasisEvidence Clone(
        SuccessionCandidateBasisEvidence value) => value with { };

    private static SuccessionCandidateSetEntry Clone(
        SuccessionCandidateSetEntry value) => value with
        {
            RecognizedBases = value.RecognizedBases.Select(Clone).ToArray(),
        };

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        ValidateCalendar(value);
        if (value.Date.CompareTo(calendar.Date) < 0 || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Character-succession campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    public CommandValidationResult ValidateAction(
        EntityId actingCharacterId,
        CharacterSuccessionActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterSuccessionActionResolvedEventPayload PlanAction(
        EntityId actingCharacterId,
        CharacterSuccessionActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        ThrowIfInvalid(issues);
        ValidateId(commandId, "Character-succession command ID");
        ValidateId(eventId, "Character-succession event ID");
        if (eventId != CharacterSuccessionIds.DeriveActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                $"Character-succession event ID '{eventId}' does not match command '{commandId}'.");
        }

        if (HasRetainedLifecycleIdentity(commandId)
            || HasRetainedLifecycleIdentity(eventId))
        {
            throw new SimulationValidationException(
                $"Character-succession command/event identity '{commandId}'/'{eventId}' is already retained.");
        }

        HeirDesignationState? current = GetStoredCurrent(actingCharacterId);
        ICharacterSuccessionActionOutcome outcome = payload.Action switch
        {
            DesignateHeirAction designate when current is null =>
                new HeirDesignatedOutcome(CreateActiveDesignation(
                    actingCharacterId,
                    designate.HeirCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            DesignateHeirAction designate => new HeirDesignationReplacedOutcome(
                ResolveDesignation(
                    current,
                    HeirDesignationStatus.Replaced,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId),
                CreateActiveDesignation(
                    actingCharacterId,
                    designate.HeirCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            RevokeHeirDesignationAction => new HeirDesignationRevokedOutcome(
                ResolveDesignation(
                    current!,
                    HeirDesignationStatus.Revoked,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            _ => throw new SimulationValidationException(
                "Unsupported character-succession action type."),
        };

        return new CharacterSuccessionActionResolvedEventPayload(
            actingCharacterId,
            Clone(payload.Action),
            Clone(outcome));
    }

    public void PrevalidateOutcome(
        CharacterSuccessionActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => _ = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);

    internal CharacterSuccessionWorldUpdatePlan PrepareOutcome(
        CharacterSuccessionActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null)
        {
            throw new SimulationValidationException(
                "Character-succession action outcome payload cannot be null.");
        }

        CharacterSuccessionActionResolvedEventPayload expected = PlanAction(
            payload.ActingCharacterId,
            new CharacterSuccessionActionCommandPayload(payload.Action),
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        string expectedJson = JsonSerializer.Serialize(expected, SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Character-succession action outcome does not match the exact deterministic plan.");
        }

        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterSuccessionWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            candidateCalendar);
        candidate.CommitOutcome(payload.Outcome);
        return new CharacterSuccessionWorldUpdatePlan(candidate);
    }

    public CommandValidationResult ValidateClaimAction(
        EntityId actingCharacterId,
        CharacterSuccessionClaimActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateClaimActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterSuccessionClaimActionResolvedEventPayload PlanClaimAction(
        EntityId actingCharacterId,
        CharacterSuccessionClaimActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        List<ValidationIssue> issues = [];
        ValidateClaimActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        ThrowIfInvalid(issues);
        ValidateId(commandId, "Succession-claim command ID");
        ValidateId(eventId, "Succession-claim event ID");
        if (eventId != CharacterSuccessionIds.DeriveClaimActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                $"Succession-claim event ID '{eventId}' does not match command '{commandId}'.");
        }

        if (HasRetainedLifecycleIdentity(commandId)
            || HasRetainedLifecycleIdentity(eventId))
        {
            throw new SimulationValidationException(
                $"Succession-claim command/event identity '{commandId}'/'{eventId}' is already retained.");
        }

        ICharacterSuccessionClaimActionOutcome outcome = payload.Action switch
        {
            AssertSuccessionClaimAction assertion =>
                new SuccessionClaimAssertedOutcome(CreateActiveClaim(
                    assertion.SubjectCharacterId,
                    actingCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            WithdrawSuccessionClaimAction withdrawal =>
                new SuccessionClaimWithdrawnOutcome(WithdrawClaim(
                    GetStoredActiveClaim(
                        withdrawal.SubjectCharacterId,
                        actingCharacterId)!,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            _ => throw new SimulationValidationException(
                "Unsupported succession-claim action type."),
        };

        return new CharacterSuccessionClaimActionResolvedEventPayload(
            actingCharacterId,
            Clone(payload.Action),
            Clone(outcome));
    }

    public void PrevalidateClaimOutcome(
        CharacterSuccessionClaimActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => _ = PrepareClaimOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);

    internal CharacterSuccessionWorldUpdatePlan PrepareClaimOutcome(
        CharacterSuccessionClaimActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null)
        {
            throw new SimulationValidationException(
                "Succession-claim action outcome payload cannot be null.");
        }

        CharacterSuccessionClaimActionResolvedEventPayload expected = PlanClaimAction(
            payload.ActingCharacterId,
            new CharacterSuccessionClaimActionCommandPayload(payload.Action),
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        string expectedJson = JsonSerializer.Serialize(expected, SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Succession-claim action outcome does not match the exact deterministic plan.");
        }

        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterSuccessionWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            candidateCalendar);
        candidate.CommitClaimOutcome(payload.Outcome);
        return new CharacterSuccessionWorldUpdatePlan(candidate);
    }

    public CommandValidationResult ValidateSupportAction(
        EntityId actingCharacterId,
        CharacterSuccessionSupportActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateSupportActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterSuccessionSupportActionResolvedEventPayload PlanSupportAction(
        EntityId actingCharacterId,
        CharacterSuccessionSupportActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        List<ValidationIssue> issues = [];
        ValidateSupportActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues);
        ThrowIfInvalid(issues);
        ValidateId(commandId, "Succession-support command ID");
        ValidateId(eventId, "Succession-support event ID");
        if (eventId != CharacterSuccessionIds.DeriveSupportActionEventId(
                resolutionDate,
                commandId))
        {
            throw new SimulationValidationException(
                $"Succession-support event ID '{eventId}' does not match command '{commandId}'.");
        }

        if (HasRetainedLifecycleIdentity(commandId)
            || HasRetainedLifecycleIdentity(eventId))
        {
            throw new SimulationValidationException(
                $"Succession-support command/event identity '{commandId}'/'{eventId}' is already retained.");
        }

        ICharacterSuccessionSupportActionOutcome outcome = payload.Action switch
        {
            DeclareSuccessionSupportAction declaration
                when GetStoredCurrentSupport(
                    declaration.SubjectId,
                    actingCharacterId) is null =>
                new SuccessionSupportDeclaredOutcome(CreateActiveSupport(
                    declaration.SubjectId,
                    actingCharacterId,
                    declaration.SupportedCandidateId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            DeclareSuccessionSupportAction declaration =>
                new SuccessionSupportReplacedOutcome(
                    ResolveSupport(
                        GetStoredCurrentSupport(
                            declaration.SubjectId,
                            actingCharacterId)!,
                        SuccessionSupportStatus.Replaced,
                        resolutionDate,
                        authoritativeTurnIndex,
                        commandId,
                        eventId),
                    CreateActiveSupport(
                        declaration.SubjectId,
                        actingCharacterId,
                        declaration.SupportedCandidateId,
                        resolutionDate,
                        authoritativeTurnIndex,
                        commandId,
                        eventId)),
            WithdrawSuccessionSupportAction withdrawal =>
                new SuccessionSupportWithdrawnOutcome(ResolveSupport(
                    GetStoredCurrentSupport(
                        withdrawal.SubjectId,
                        actingCharacterId)!,
                    SuccessionSupportStatus.Withdrawn,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId,
                    eventId)),
            _ => throw new SimulationValidationException(
                "Unsupported succession-support action type."),
        };

        return new CharacterSuccessionSupportActionResolvedEventPayload(
            actingCharacterId,
            Clone(payload.Action),
            Clone(outcome));
    }

    public void PrevalidateSupportOutcome(
        CharacterSuccessionSupportActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => _ = PrepareSupportOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);

    internal CharacterSuccessionWorldUpdatePlan PrepareSupportOutcome(
        CharacterSuccessionSupportActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null)
        {
            throw new SimulationValidationException(
                "Succession-support action outcome payload cannot be null.");
        }

        CharacterSuccessionSupportActionResolvedEventPayload expected =
            PlanSupportAction(
                payload.ActingCharacterId,
                new CharacterSuccessionSupportActionCommandPayload(payload.Action),
                resolutionDate,
                authoritativeTurnIndex,
                commandId,
                eventId);
        string expectedJson = JsonSerializer.Serialize(
            expected,
            SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(
            payload,
            SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Succession-support action outcome does not match the exact deterministic plan.");
        }

        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterSuccessionWorldState candidate = new(
            CaptureSnapshot(),
            characters,
            candidateCalendar);
        candidate.CommitSupportOutcome(payload.Outcome);
        return new CharacterSuccessionWorldUpdatePlan(candidate);
    }

    internal void ApplyPrepared(CharacterSuccessionWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-succession update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private void CommitOutcome(ICharacterSuccessionActionOutcome outcome)
    {
        switch (outcome)
        {
            case HeirDesignatedOutcome designated:
                AddActive(designated.CurrentDesignation);
                break;
            case HeirDesignationReplacedOutcome replaced:
                ReplaceActive(replaced.PreviousDesignation);
                AddActive(replaced.CurrentDesignation);
                break;
            case HeirDesignationRevokedOutcome revoked:
                ReplaceActive(revoked.PreviousDesignation);
                break;
            default:
                throw new SimulationValidationException(
                    "Unsupported character-succession outcome type.");
        }

        EnforceRetentionBound(GetOutcomeDesignator(outcome));
        ValidateRetentionBounds();
    }

    private void CommitClaimOutcome(ICharacterSuccessionClaimActionOutcome outcome)
    {
        EntityId subjectCharacterId;
        switch (outcome)
        {
            case SuccessionClaimAssertedOutcome asserted:
                AddActiveClaim(asserted.CurrentClaim);
                subjectCharacterId = asserted.CurrentClaim.SubjectCharacterId;
                break;
            case SuccessionClaimWithdrawnOutcome withdrawn:
                ReplaceActiveClaim(withdrawn.PreviousClaim);
                subjectCharacterId = withdrawn.PreviousClaim.SubjectCharacterId;
                break;
            default:
                throw new SimulationValidationException(
                    "Unsupported succession-claim outcome type.");
        }

        EnforceClaimRetentionBound(subjectCharacterId);
        ValidateRetentionBounds();
    }

    private void CommitSupportOutcome(ICharacterSuccessionSupportActionOutcome outcome)
    {
        EntityId subjectId;
        switch (outcome)
        {
            case SuccessionSupportDeclaredOutcome declared:
                AddActiveSupport(declared.CurrentSupport);
                subjectId = declared.CurrentSupport.SubjectId;
                break;
            case SuccessionSupportReplacedOutcome replaced:
                ReplaceActiveSupport(replaced.PreviousSupport);
                AddActiveSupport(replaced.CurrentSupport);
                subjectId = replaced.CurrentSupport.SubjectId;
                break;
            case SuccessionSupportWithdrawnOutcome withdrawn:
                ReplaceActiveSupport(withdrawn.PreviousSupport);
                subjectId = withdrawn.PreviousSupport.SubjectId;
                break;
            default:
                throw new SimulationValidationException(
                    "Unsupported succession-support outcome type.");
        }

        EnforceSupportRetentionBound(subjectId);
        ValidateRetentionBounds();
    }

    private void AddActiveSupport(SuccessionSupportState support)
    {
        ValidateSupport(support);
        if (support.Status != SuccessionSupportStatus.Active
            || !supports.TryAdd(support.SupportId, Clone(support))
            || !activeSupportByPair.TryAdd(
                (support.SubjectId, support.SupporterId),
                support.SupportId))
        {
            throw new SimulationValidationException(
                $"Succession support '{support.SupportId}' cannot become active.");
        }

        if (supports.Values.Count(item =>
                item.SubjectId == support.SubjectId
                && item.Status == SuccessionSupportStatus.Active)
                > CharacterSuccessionLimits.MaximumActiveSupportsPerSubject
            || supports.Values.Count(item =>
                item.SupporterId == support.SupporterId
                && item.Status == SuccessionSupportStatus.Active)
                > CharacterSuccessionLimits.MaximumActiveSupportsPerSupporter)
        {
            throw new SimulationValidationException(
                "Succession-support active capacity was exceeded.");
        }
    }

    private void ReplaceActiveSupport(SuccessionSupportState terminal)
    {
        ValidateSupport(terminal);
        (EntityId Subject, EntityId Supporter) pair = (
            terminal.SubjectId,
            terminal.SupporterId);
        if (terminal.Status == SuccessionSupportStatus.Active
            || !activeSupportByPair.TryGetValue(pair, out EntityId activeId)
            || activeId != terminal.SupportId
            || !supports.ContainsKey(activeId))
        {
            throw new SimulationValidationException(
                $"Succession support '{terminal.SupportId}' is not the exact active record.");
        }

        supports[activeId] = Clone(terminal);
        activeSupportByPair.Remove(pair);
    }

    private void EnforceSupportRetentionBound(EntityId subjectId)
    {
        while (supports.Values.Count(item =>
                   item.SubjectId == subjectId
                   && item.Status != SuccessionSupportStatus.Active)
               > CharacterSuccessionLimits.RecentTerminalSupportsPerSubject)
        {
            SuccessionSupportState[] records = supports.Values
                .Where(item => item.SubjectId == subjectId)
                .ToArray();
            SuccessionSupportState evicted = records
                .Where(item => item.Status != SuccessionSupportStatus.Active
                    && !records.Any(predecessor => IsExactSupportSuccessor(
                        predecessor,
                        item)))
                .OrderBy(item => item.ResolutionTurnIndex)
                .ThenBy(item => item.ResolutionDate)
                .ThenBy(item => item.SupportId)
                .FirstOrDefault()
                ?? throw new SimulationValidationException(
                    $"Succession-support lifecycle for '{subjectId}' has no foldable root record.");
            FoldSupport(evicted);
            supports.Remove(evicted.SupportId);
        }
    }

    private void FoldSupport(SuccessionSupportState support)
    {
        CampaignDate resolutionDate = support.ResolutionDate
            ?? throw new SimulationValidationException(
                "Terminal succession support is missing its resolution date.");
        SuccessionSupportHistoryAggregate aggregate = supportHistory.TryGetValue(
            support.SubjectId,
            out SuccessionSupportHistoryAggregate? stored)
            ? stored
            : new(
                CharacterSuccessionContractVersions.SupportHistory,
                support.SubjectId,
                0,
                0,
                resolutionDate,
                resolutionDate);
        try
        {
            aggregate = support.Status switch
            {
                SuccessionSupportStatus.Replaced => aggregate with
                {
                    FoldedReplacedCount = checked(aggregate.FoldedReplacedCount + 1),
                },
                SuccessionSupportStatus.Withdrawn => aggregate with
                {
                    FoldedWithdrawnCount = checked(aggregate.FoldedWithdrawnCount + 1),
                },
                _ => throw new SimulationValidationException(
                    "Only terminal succession supports can be folded."),
            };
            aggregate = aggregate with
            {
                EarliestDate = Earlier(aggregate.EarliestDate, resolutionDate),
                LatestDate = Later(aggregate.LatestDate, resolutionDate),
            };
            _ = aggregate.TotalFoldedCount;
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Succession-support history for '{support.SubjectId}' exceeds Int64 capacity: {exception.Message}");
        }

        supportHistory[support.SubjectId] = aggregate;
    }

    private void AddActiveClaim(SuccessionClaimState claim)
    {
        ValidateClaim(claim);
        if (claim.Status != SuccessionClaimStatus.Active
            || !claims.TryAdd(claim.ClaimId, Clone(claim))
            || !activeClaimByPair.TryAdd(
                (claim.SubjectCharacterId, claim.ClaimantCharacterId),
                claim.ClaimId))
        {
            throw new SimulationValidationException(
                $"Succession claim '{claim.ClaimId}' cannot become active.");
        }

        if (claims.Values.Count(item =>
                item.SubjectCharacterId == claim.SubjectCharacterId
                && item.Status == SuccessionClaimStatus.Active)
                > CharacterSuccessionLimits.MaximumActiveClaimsPerSubject
            || claims.Values.Count(item =>
                item.ClaimantCharacterId == claim.ClaimantCharacterId
                && item.Status == SuccessionClaimStatus.Active)
                > CharacterSuccessionLimits.MaximumActiveClaimsPerClaimant)
        {
            throw new SimulationValidationException(
                "Succession-claim active capacity was exceeded.");
        }
    }

    private void ReplaceActiveClaim(SuccessionClaimState withdrawn)
    {
        ValidateClaim(withdrawn);
        (EntityId Subject, EntityId Claimant) pair = (
            withdrawn.SubjectCharacterId,
            withdrawn.ClaimantCharacterId);
        if (withdrawn.Status != SuccessionClaimStatus.Withdrawn
            || !activeClaimByPair.TryGetValue(pair, out EntityId activeId)
            || activeId != withdrawn.ClaimId
            || !claims.ContainsKey(activeId))
        {
            throw new SimulationValidationException(
                $"Succession claim '{withdrawn.ClaimId}' is not the exact active record.");
        }

        claims[activeId] = Clone(withdrawn);
        activeClaimByPair.Remove(pair);
    }

    private void EnforceClaimRetentionBound(EntityId subjectCharacterId)
    {
        while (claims.Values.Count(item =>
                   item.SubjectCharacterId == subjectCharacterId
                   && item.Status == SuccessionClaimStatus.Withdrawn)
               > CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject)
        {
            SuccessionClaimState evicted = claims.Values
                .Where(item => item.SubjectCharacterId == subjectCharacterId
                    && item.Status == SuccessionClaimStatus.Withdrawn)
                .OrderBy(item => item.WithdrawalTurnIndex)
                .ThenBy(item => item.WithdrawalDate)
                .ThenBy(item => item.ClaimId)
                .First();
            FoldClaim(evicted);
            claims.Remove(evicted.ClaimId);
        }
    }

    private void FoldClaim(SuccessionClaimState claim)
    {
        CampaignDate withdrawalDate = claim.WithdrawalDate
            ?? throw new SimulationValidationException(
                "Withdrawn succession claim is missing its withdrawal date.");
        SuccessionClaimHistoryAggregate aggregate = claimHistory.TryGetValue(
            claim.SubjectCharacterId,
            out SuccessionClaimHistoryAggregate? stored)
            ? stored
            : new(
                CharacterSuccessionContractVersions.ClaimHistory,
                claim.SubjectCharacterId,
                0,
                withdrawalDate,
                withdrawalDate);
        try
        {
            aggregate = aggregate with
            {
                FoldedWithdrawnCount = checked(aggregate.FoldedWithdrawnCount + 1),
                EarliestDate = Earlier(aggregate.EarliestDate, withdrawalDate),
                LatestDate = Later(aggregate.LatestDate, withdrawalDate),
            };
            _ = aggregate.TotalFoldedCount;
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Succession-claim history for '{claim.SubjectCharacterId}' exceeds Int64 capacity: {exception.Message}");
        }

        claimHistory[claim.SubjectCharacterId] = aggregate;
    }

    private void AddActive(HeirDesignationState designation)
    {
        ValidateDesignation(designation);
        if (designation.Status != HeirDesignationStatus.Active
            || !designations.TryAdd(designation.DesignationId, Clone(designation))
            || !activeByDesignator.TryAdd(
                designation.DesignatorCharacterId,
                designation.DesignationId))
        {
            throw new SimulationValidationException(
                $"Heir designation '{designation.DesignationId}' cannot become active.");
        }
    }

    private void ReplaceActive(HeirDesignationState terminal)
    {
        ValidateDesignation(terminal);
        if (terminal.Status == HeirDesignationStatus.Active
            || !activeByDesignator.TryGetValue(
                terminal.DesignatorCharacterId,
                out EntityId activeId)
            || activeId != terminal.DesignationId
            || !designations.ContainsKey(activeId))
        {
            throw new SimulationValidationException(
                $"Heir designation '{terminal.DesignationId}' is not the exact active record.");
        }

        designations[activeId] = Clone(terminal);
        activeByDesignator.Remove(terminal.DesignatorCharacterId);
    }

    private void EnforceRetentionBound(EntityId designatorCharacterId)
    {
        while (designations.Values.Count(item =>
                   item.DesignatorCharacterId == designatorCharacterId
                   && item.Status != HeirDesignationStatus.Active)
               > CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter)
        {
            HeirDesignationState[] records = designations.Values
                .Where(item => item.DesignatorCharacterId == designatorCharacterId)
                .ToArray();
            HeirDesignationState evicted = records
                .Where(item => item.Status != HeirDesignationStatus.Active
                    && !records.Any(predecessor => IsExactSuccessor(
                        predecessor,
                        item)))
                .OrderBy(item => item.ResolutionTurnIndex)
                .ThenBy(item => item.ResolutionDate)
                .ThenBy(item => item.DesignationId)
                .FirstOrDefault()
                ?? throw new SimulationValidationException(
                    $"Heir-designation lifecycle for '{designatorCharacterId}' has no foldable root record.");
            Fold(evicted);
            designations.Remove(evicted.DesignationId);
        }
    }

    private void Fold(HeirDesignationState designation)
    {
        CampaignDate resolutionDate = designation.ResolutionDate
            ?? throw new SimulationValidationException(
                "Terminal heir designation is missing its resolution date.");
        HeirDesignationHistoryAggregate aggregate = history.TryGetValue(
            designation.DesignatorCharacterId,
            out HeirDesignationHistoryAggregate? stored)
            ? stored
            : new(
                CharacterSuccessionContractVersions.State,
                designation.DesignatorCharacterId,
                0,
                0,
                resolutionDate,
                resolutionDate);
        try
        {
            aggregate = designation.Status switch
            {
                HeirDesignationStatus.Replaced => aggregate with
                {
                    FoldedReplacedCount = checked(aggregate.FoldedReplacedCount + 1),
                },
                HeirDesignationStatus.Revoked => aggregate with
                {
                    FoldedRevokedCount = checked(aggregate.FoldedRevokedCount + 1),
                },
                _ => throw new SimulationValidationException(
                    "Only terminal heir designations can be folded."),
            };
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Heir-designation history for '{designation.DesignatorCharacterId}' exceeds Int64 capacity: {exception.Message}");
        }

        history[designation.DesignatorCharacterId] = aggregate with
        {
            EarliestDate = Earlier(aggregate.EarliestDate, resolutionDate),
            LatestDate = Later(aggregate.LatestDate, resolutionDate),
        };
        try
        {
            _ = history[designation.DesignatorCharacterId].TotalFoldedCount;
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Heir-designation history for '{designation.DesignatorCharacterId}' exceeds Int64 capacity: {exception.Message}");
        }
    }

    private void ValidateActionEnvelope(
        EntityId actingCharacterId,
        CharacterSuccessionActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        ICollection<ValidationIssue> issues)
    {
        if (!resolutionDate.IsValid)
        {
            issues.Add(new("invalid_resolution_date", "Heir-designation resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new(
                "past_resolution_date",
                "Heir-designation resolution date precedes succession state."));
        }

        if (authoritativeTurnIndex < calendar.TurnIndex)
        {
            issues.Add(new(
                "past_turn_index",
                "Heir-designation action turn precedes succession state."));
        }

        AuthoritativeCharacterProfile? actor = ValidateActionCharacter(
            actingCharacterId,
            resolutionDate,
            "designator",
            issues,
            requireAgency: true);
        if (payload?.Action is null)
        {
            issues.Add(new("invalid_payload", "Character-succession action cannot be null."));
            return;
        }

        HeirDesignationState? current = actingCharacterId.IsValid
            ? GetStoredCurrent(actingCharacterId)
            : null;
        switch (payload.Action)
        {
            case DesignateHeirAction designate:
                _ = ValidateActionCharacter(
                    designate.HeirCharacterId,
                    resolutionDate,
                    "heir",
                    issues,
                    requireAgency: false);
                if (actor is not null && designate.HeirCharacterId == actingCharacterId)
                {
                    issues.Add(new(
                        "self_designation",
                        "A character cannot designate themself as heir."));
                }

                ValidateExpectedCurrent(
                    designate.ExpectedCurrentDesignationId,
                    current,
                    issues);
                if (current?.HeirCharacterId == designate.HeirCharacterId)
                {
                    issues.Add(new(
                        "heir_already_designated",
                        "The requested heir is already the current designation."));
                }

                if (current is not null)
                {
                    ValidateFoldCapacity(
                        current,
                        HeirDesignationStatus.Replaced,
                        resolutionDate,
                        authoritativeTurnIndex,
                        issues);
                }

                break;
            case RevokeHeirDesignationAction revoke:
                if (!revoke.ExpectedCurrentDesignationId.IsValid)
                {
                    issues.Add(new(
                        "invalid_expected_designation",
                        "Heir-designation revocation requires a valid expected current designation ID."));
                }

                ValidateExpectedCurrent(
                    revoke.ExpectedCurrentDesignationId,
                    current,
                    issues);
                if (current is not null)
                {
                    ValidateFoldCapacity(
                        current,
                        HeirDesignationStatus.Revoked,
                        resolutionDate,
                        authoritativeTurnIndex,
                        issues);
                }
                break;
            default:
                issues.Add(new(
                    "unsupported_character_succession_action",
                    "Only designate_heir.v1 and revoke_heir_designation.v1 are registered."));
                break;
        }
    }

    private void ValidateClaimActionEnvelope(
        EntityId actingCharacterId,
        CharacterSuccessionClaimActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        ICollection<ValidationIssue> issues)
    {
        if (!resolutionDate.IsValid)
        {
            issues.Add(new(
                "invalid_resolution_date",
                "Succession-claim resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new(
                "past_resolution_date",
                "Succession-claim resolution date precedes succession state."));
        }

        if (authoritativeTurnIndex < calendar.TurnIndex)
        {
            issues.Add(new(
                "past_turn_index",
                "Succession-claim action turn precedes succession state."));
        }

        AuthoritativeCharacterProfile? claimant = ValidateClaimParticipant(
            actingCharacterId,
            resolutionDate,
            "claimant",
            issues,
            requireAgency: true);
        if (payload?.Action is null)
        {
            issues.Add(new(
                "invalid_payload",
                "Character-succession claim action cannot be null."));
            return;
        }

        EntityId subjectCharacterId = payload.Action switch
        {
            AssertSuccessionClaimAction assertion => assertion.SubjectCharacterId,
            WithdrawSuccessionClaimAction withdrawal => withdrawal.SubjectCharacterId,
            _ => default,
        };
        AuthoritativeCharacterProfile? subject = ValidateClaimParticipant(
            subjectCharacterId,
            resolutionDate,
            "subject",
            issues,
            requireAgency: false);
        if (claimant is not null && subject is not null
            && actingCharacterId == subjectCharacterId)
        {
            issues.Add(new(
                "self_succession_claim",
                "A character cannot assert a succession claim to themself."));
        }

        SuccessionClaimState? current = actingCharacterId.IsValid
            && subjectCharacterId.IsValid
                ? GetStoredActiveClaim(subjectCharacterId, actingCharacterId)
                : null;
        switch (payload.Action)
        {
            case AssertSuccessionClaimAction:
                if (current is not null)
                {
                    issues.Add(new(
                        "succession_claim_already_active",
                        "The claimant already has an active claim to this subject."));
                }

                if (claims.Values.Count(item =>
                        item.SubjectCharacterId == subjectCharacterId
                        && item.Status == SuccessionClaimStatus.Active)
                    >= CharacterSuccessionLimits.MaximumActiveClaimsPerSubject)
                {
                    issues.Add(new(
                        "succession_claim_subject_capacity",
                        "The subject has reached the active succession-claim capacity."));
                }

                if (claims.Values.Count(item =>
                        item.ClaimantCharacterId == actingCharacterId
                        && item.Status == SuccessionClaimStatus.Active)
                    >= CharacterSuccessionLimits.MaximumActiveClaimsPerClaimant)
                {
                    issues.Add(new(
                        "succession_claim_claimant_capacity",
                        "The claimant has reached the active succession-claim capacity."));
                }

                break;
            case WithdrawSuccessionClaimAction withdrawal:
                if (!withdrawal.ExpectedCurrentClaimId.IsValid)
                {
                    issues.Add(new(
                        "invalid_expected_succession_claim",
                        "Succession-claim withdrawal requires a valid expected current claim ID."));
                }

                if (current?.ClaimId != withdrawal.ExpectedCurrentClaimId)
                {
                    issues.Add(new(
                        "stale_succession_claim",
                        "Expected current succession claim does not match authoritative state."));
                }

                if (current is not null)
                {
                    ValidateClaimFoldCapacity(current, issues);
                }

                break;
            default:
                issues.Add(new(
                    "unsupported_character_succession_claim_action",
                    "Only assert_succession_claim.v1 and withdraw_succession_claim.v1 are registered."));
                break;
        }
    }

    private void ValidateClaimFoldCapacity(
        SuccessionClaimState current,
        ICollection<ValidationIssue> issues)
    {
        int terminalCount = claims.Values.Count(item =>
            item.SubjectCharacterId == current.SubjectCharacterId
            && item.Status == SuccessionClaimStatus.Withdrawn);
        if (terminalCount < CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject)
        {
            return;
        }

        if (claimHistory.TryGetValue(
                current.SubjectCharacterId,
                out SuccessionClaimHistoryAggregate? aggregate))
        {
            try
            {
                _ = checked(aggregate.FoldedWithdrawnCount + 1);
            }
            catch (OverflowException)
            {
                issues.Add(new(
                    "succession_claim_history_overflow",
                    "Succession-claim history cannot fold another withdrawn record without exceeding Int64 capacity."));
            }
        }
    }

    private void ValidateSupportActionEnvelope(
        EntityId actingCharacterId,
        CharacterSuccessionSupportActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        ICollection<ValidationIssue> issues)
    {
        if (!resolutionDate.IsValid)
        {
            issues.Add(new(
                "invalid_resolution_date",
                "Succession-support resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new(
                "past_resolution_date",
                "Succession-support resolution date precedes succession state."));
        }

        if (authoritativeTurnIndex < calendar.TurnIndex)
        {
            issues.Add(new(
                "past_turn_index",
                "Succession-support action turn precedes succession state."));
        }

        AuthoritativeCharacterProfile? supporter = ValidateSupportParticipant(
            actingCharacterId,
            resolutionDate,
            "supporter",
            issues,
            requireAgency: true,
            requireLiving: true);
        if (payload?.Action is null)
        {
            issues.Add(new(
                "invalid_payload",
                "Character-succession support action cannot be null."));
            return;
        }

        EntityId subjectId = payload.Action switch
        {
            DeclareSuccessionSupportAction declaration => declaration.SubjectId,
            WithdrawSuccessionSupportAction withdrawal => withdrawal.SubjectId,
            _ => default,
        };
        AuthoritativeCharacterProfile? subject = ValidateSupportParticipant(
            subjectId,
            resolutionDate,
            "subject",
            issues,
            requireAgency: false,
            requireLiving: false);
        SuccessionSupportState? current = actingCharacterId.IsValid
            && subjectId.IsValid
                ? GetStoredCurrentSupport(subjectId, actingCharacterId)
                : null;

        switch (payload.Action)
        {
            case DeclareSuccessionSupportAction declaration:
                AuthoritativeCharacterProfile? candidate = ValidateSupportParticipant(
                    declaration.SupportedCandidateId,
                    resolutionDate,
                    "supported_candidate",
                    issues,
                    requireAgency: false,
                    requireLiving: true);
                if (supporter is not null
                    && subject is not null
                    && actingCharacterId == subjectId)
                {
                    issues.Add(new(
                        "supporter_is_subject",
                        "A succession-support supporter must differ from the subject."));
                }

                if (supporter is not null
                    && candidate is not null
                    && actingCharacterId == declaration.SupportedCandidateId)
                {
                    issues.Add(new(
                        "supporter_is_candidate",
                        "A succession-support supporter cannot support themself."));
                }

                if (subject is not null
                    && candidate is not null
                    && subjectId == declaration.SupportedCandidateId)
                {
                    issues.Add(new(
                        "subject_is_candidate",
                        "A succession-support candidate must differ from the subject."));
                }

                ValidateExpectedCurrentSupport(
                    declaration.ExpectedCurrentSupportId,
                    current,
                    issues);
                if (current?.SupportedCandidateId
                    == declaration.SupportedCandidateId)
                {
                    issues.Add(new(
                        "succession_support_candidate_unchanged",
                        "The requested candidate is already the current succession support."));
                }

                if (current is null)
                {
                    if (supports.Values.Count(item =>
                            item.SubjectId == subjectId
                            && item.Status == SuccessionSupportStatus.Active)
                        >= CharacterSuccessionLimits.MaximumActiveSupportsPerSubject)
                    {
                        issues.Add(new(
                            "succession_support_subject_capacity",
                            "The subject has reached the active succession-support capacity."));
                    }

                    if (supports.Values.Count(item =>
                            item.SupporterId == actingCharacterId
                            && item.Status == SuccessionSupportStatus.Active)
                        >= CharacterSuccessionLimits.MaximumActiveSupportsPerSupporter)
                    {
                        issues.Add(new(
                            "succession_support_supporter_capacity",
                            "The supporter has reached the active succession-support capacity."));
                    }
                }
                else
                {
                    ValidateSupportFoldCapacity(
                        current,
                        SuccessionSupportStatus.Replaced,
                        resolutionDate,
                        authoritativeTurnIndex,
                        issues);
                }

                break;
            case WithdrawSuccessionSupportAction withdrawal:
                if (!withdrawal.ExpectedCurrentSupportId.IsValid)
                {
                    issues.Add(new(
                        "invalid_expected_succession_support",
                        "Succession-support withdrawal requires a valid expected current support ID."));
                }

                if (current?.SupportId != withdrawal.ExpectedCurrentSupportId)
                {
                    issues.Add(new(
                        "stale_succession_support",
                        "Expected current succession support does not match authoritative state."));
                }

                if (current is not null)
                {
                    ValidateSupportFoldCapacity(
                        current,
                        SuccessionSupportStatus.Withdrawn,
                        resolutionDate,
                        authoritativeTurnIndex,
                        issues);
                }

                break;
            default:
                issues.Add(new(
                    "unsupported_character_succession_support_action",
                    "Only declare_succession_support.v1 and withdraw_succession_support.v1 are registered."));
                break;
        }
    }

    private void ValidateSupportFoldCapacity(
        SuccessionSupportState current,
        SuccessionSupportStatus terminalStatus,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        ICollection<ValidationIssue> issues)
    {
        SuccessionSupportState[] existingTerminal = supports.Values
            .Where(item => item.SubjectId == current.SubjectId
                && item.Status != SuccessionSupportStatus.Active)
            .ToArray();
        if (existingTerminal.Length
            < CharacterSuccessionLimits.RecentTerminalSupportsPerSubject)
        {
            return;
        }

        SuccessionSupportState pendingTerminal = current with
        {
            Status = terminalStatus,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = resolutionTurnIndex,
        };
        SuccessionSupportState[] candidateRecords = supports.Values
            .Where(item => item.SubjectId == current.SubjectId
                && item.SupportId != current.SupportId)
            .Append(pendingTerminal)
            .ToArray();
        SuccessionSupportState evicted = candidateRecords
            .Where(item => item.Status != SuccessionSupportStatus.Active
                && !candidateRecords.Any(predecessor => IsExactSupportSuccessor(
                    predecessor,
                    item)))
            .OrderBy(item => item.ResolutionTurnIndex)
            .ThenBy(item => item.ResolutionDate)
            .ThenBy(item => item.SupportId)
            .FirstOrDefault()
            ?? throw new SimulationValidationException(
                "Succession-support lifecycle has no foldable root record.");
        SuccessionSupportHistoryAggregate aggregate = supportHistory.TryGetValue(
            current.SubjectId,
            out SuccessionSupportHistoryAggregate? stored)
            ? stored
            : new(
                CharacterSuccessionContractVersions.SupportHistory,
                current.SubjectId,
                0,
                0,
                evicted.ResolutionDate!.Value,
                evicted.ResolutionDate.Value);
        try
        {
            long replaced = checked(aggregate.FoldedReplacedCount
                + (evicted.Status == SuccessionSupportStatus.Replaced ? 1 : 0));
            long withdrawn = checked(aggregate.FoldedWithdrawnCount
                + (evicted.Status == SuccessionSupportStatus.Withdrawn ? 1 : 0));
            _ = checked(replaced + withdrawn);
        }
        catch (OverflowException)
        {
            issues.Add(new(
                "succession_support_history_overflow",
                "Succession-support history cannot fold another terminal record without exceeding Int64 capacity."));
        }
    }

    private static void ValidateExpectedCurrentSupport(
        EntityId? expectedCurrentSupportId,
        SuccessionSupportState? current,
        ICollection<ValidationIssue> issues)
    {
        if (expectedCurrentSupportId is EntityId expected && !expected.IsValid)
        {
            issues.Add(new(
                "invalid_expected_succession_support",
                "Expected current succession-support ID is invalid."));
            return;
        }

        if (expectedCurrentSupportId is null && current is null)
        {
            return;
        }

        if (expectedCurrentSupportId is EntityId exact
            && current?.SupportId == exact)
        {
            return;
        }

        issues.Add(new(
            "stale_succession_support",
            "Expected current succession support does not match authoritative state."));
    }

    private void ValidateFoldCapacity(
        HeirDesignationState current,
        HeirDesignationStatus terminalStatus,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        ICollection<ValidationIssue> issues)
    {
        HeirDesignationState[] existingTerminal = designations.Values
            .Where(item => item.DesignatorCharacterId == current.DesignatorCharacterId
                && item.Status != HeirDesignationStatus.Active)
            .ToArray();
        if (existingTerminal.Length < CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter)
        {
            return;
        }

        HeirDesignationState pendingTerminal = current with
        {
            Status = terminalStatus,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = resolutionTurnIndex,
        };
        HeirDesignationState[] candidateRecords = designations.Values
            .Where(item => item.DesignatorCharacterId == current.DesignatorCharacterId
                && item.DesignationId != current.DesignationId)
            .Append(pendingTerminal)
            .ToArray();
        HeirDesignationState evicted = candidateRecords
            .Where(item => item.Status != HeirDesignationStatus.Active
                && !candidateRecords.Any(predecessor => IsExactSuccessor(
                    predecessor,
                    item)))
            .OrderBy(item => item.ResolutionTurnIndex)
            .ThenBy(item => item.ResolutionDate)
            .ThenBy(item => item.DesignationId)
            .FirstOrDefault()
            ?? throw new SimulationValidationException(
                "Heir-designation lifecycle has no foldable root record.");
        HeirDesignationHistoryAggregate aggregate = history.TryGetValue(
            current.DesignatorCharacterId,
            out HeirDesignationHistoryAggregate? stored)
            ? stored
            : new(
                CharacterSuccessionContractVersions.State,
                current.DesignatorCharacterId,
                0,
                0,
                evicted.ResolutionDate!.Value,
                evicted.ResolutionDate.Value);
        try
        {
            long replaced = checked(aggregate.FoldedReplacedCount
                + (evicted.Status == HeirDesignationStatus.Replaced ? 1 : 0));
            long revoked = checked(aggregate.FoldedRevokedCount
                + (evicted.Status == HeirDesignationStatus.Revoked ? 1 : 0));
            _ = checked(replaced + revoked);
        }
        catch (OverflowException)
        {
            issues.Add(new(
                "heir_designation_history_overflow",
                "Heir-designation history cannot fold another terminal record without exceeding Int64 capacity."));
        }
    }

    private static void ValidateExpectedCurrent(
        EntityId? expectedCurrentDesignationId,
        HeirDesignationState? current,
        ICollection<ValidationIssue> issues)
    {
        if (expectedCurrentDesignationId is EntityId expected && !expected.IsValid)
        {
            issues.Add(new(
                "invalid_expected_designation",
                "Expected current heir-designation ID is invalid."));
            return;
        }

        if (expectedCurrentDesignationId is null && current is null)
        {
            return;
        }

        if (expectedCurrentDesignationId is EntityId exact
            && current?.DesignationId == exact)
        {
            return;
        }

        issues.Add(new(
            "stale_heir_designation",
            "Expected current heir designation does not match authoritative state."));
    }

    private AuthoritativeCharacterProfile? ValidateActionCharacter(
        EntityId characterId,
        CampaignDate resolutionDate,
        string role,
        ICollection<ValidationIssue> issues,
        bool requireAgency)
    {
        if (!characterId.IsValid)
        {
            issues.Add(new($"invalid_{role}", $"Heir-designation {role} ID is invalid."));
            return null;
        }

        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            issues.Add(new($"unknown_{role}", $"Heir-designation {role} '{characterId}' does not exist."));
            return null;
        }

        if (resolutionDate.IsValid && profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new($"{role}_not_born", $"Heir-designation {role} is not born by resolution."));
        }

        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new($"{role}_dead", $"Heir-designation {role} is dead."));
        }

        if (requireAgency && profile.Condition.IsIncapacitated)
        {
            issues.Add(new(
                "designator_incapacitated",
                "Heir-designation designator is incapacitated."));
        }

        if (requireAgency && profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            issues.Add(new(
                "designator_not_free",
                "Heir-designation designator is not free to act."));
        }

        return profile;
    }

    private AuthoritativeCharacterProfile? ValidateClaimParticipant(
        EntityId characterId,
        CampaignDate resolutionDate,
        string role,
        ICollection<ValidationIssue> issues,
        bool requireAgency)
    {
        if (!characterId.IsValid)
        {
            issues.Add(new(
                $"invalid_{role}",
                $"Succession-claim {role} ID is invalid."));
            return null;
        }

        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            issues.Add(new(
                $"unknown_{role}",
                $"Succession-claim {role} '{characterId}' does not exist."));
            return null;
        }

        if (resolutionDate.IsValid && profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new(
                $"{role}_not_born",
                $"Succession-claim {role} is not born by resolution."));
        }

        if (requireAgency && profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new(
                "claimant_dead",
                "Succession-claim claimant is dead."));
        }

        if (requireAgency && profile.Condition.IsIncapacitated)
        {
            issues.Add(new(
                "claimant_incapacitated",
                "Succession-claim claimant is incapacitated."));
        }

        if (requireAgency && profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            issues.Add(new(
                "claimant_not_free",
                "Succession-claim claimant is not free to act."));
        }

        return profile;
    }

    private AuthoritativeCharacterProfile? ValidateSupportParticipant(
        EntityId characterId,
        CampaignDate resolutionDate,
        string role,
        ICollection<ValidationIssue> issues,
        bool requireAgency,
        bool requireLiving)
    {
        if (!characterId.IsValid)
        {
            issues.Add(new(
                $"invalid_{role}",
                $"Succession-support {role} ID is invalid."));
            return null;
        }

        if (!characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            issues.Add(new(
                $"unknown_{role}",
                $"Succession-support {role} '{characterId}' does not exist."));
            return null;
        }

        if (resolutionDate.IsValid && profile.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new(
                $"{role}_not_born",
                $"Succession-support {role} is not born by resolution."));
        }

        if (requireLiving
            && profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new(
                $"{role}_dead",
                $"Succession-support {role} is dead."));
        }

        if (requireAgency && profile.Condition.IsIncapacitated)
        {
            issues.Add(new(
                "supporter_incapacitated",
                "Succession-support supporter is incapacitated."));
        }

        if (requireAgency
            && profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            issues.Add(new(
                "supporter_not_free",
                "Succession-support supporter is not free to act."));
        }

        return profile;
    }

    private void AddDesignations(IReadOnlyList<HeirDesignationState> source)
    {
        foreach (HeirDesignationState designation in source)
        {
            ValidateDesignation(designation);
            if (!designations.TryAdd(designation.DesignationId, Clone(designation)))
            {
                throw new SimulationValidationException(
                    $"Duplicate heir designation '{designation.DesignationId}'.");
            }

            if (designation.Status == HeirDesignationStatus.Active
                && !activeByDesignator.TryAdd(
                    designation.DesignatorCharacterId,
                    designation.DesignationId))
            {
                throw new SimulationValidationException(
                    $"Character '{designation.DesignatorCharacterId}' has multiple active heir designations.");
            }
        }
    }

    private void AddHistory(IReadOnlyList<HeirDesignationHistoryAggregate> source)
    {
        foreach (HeirDesignationHistoryAggregate aggregate in source)
        {
            if (aggregate.ContractVersion != CharacterSuccessionContractVersions.State
                || aggregate.FoldedReplacedCount < 0
                || aggregate.FoldedRevokedCount < 0
                || !aggregate.EarliestDate.IsValid
                || !aggregate.LatestDate.IsValid
                || aggregate.EarliestDate.CompareTo(aggregate.LatestDate) > 0
                || aggregate.LatestDate.CompareTo(calendar.Date) > 0)
            {
                throw new SimulationValidationException(
                    "Heir-designation history aggregate is malformed.");
            }

            AuthoritativeCharacterProfile designator = RequireCharacter(
                aggregate.DesignatorCharacterId,
                "Heir-designation history designator");
            if (designator.BirthDate.CompareTo(aggregate.EarliestDate) > 0)
            {
                throw new SimulationValidationException(
                    "Heir-designation history predates its designator's birth.");
            }

            try
            {
                if (aggregate.TotalFoldedCount <= 0)
                {
                    throw new SimulationValidationException(
                        "Heir-designation history aggregate must retain at least one folded record.");
                }
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Heir-designation history aggregate exceeds Int64 capacity: {exception.Message}");
            }

            if (!history.TryAdd(aggregate.DesignatorCharacterId, Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate heir-designation history for '{aggregate.DesignatorCharacterId}'.");
            }
        }
    }

    private void AddClaims(IReadOnlyList<SuccessionClaimState> source)
    {
        foreach (SuccessionClaimState claim in source)
        {
            ValidateClaim(claim);
            if (!claims.TryAdd(claim.ClaimId, Clone(claim)))
            {
                throw new SimulationValidationException(
                    $"Duplicate succession claim '{claim.ClaimId}'.");
            }

            if (claim.Status == SuccessionClaimStatus.Active
                && !activeClaimByPair.TryAdd(
                    (claim.SubjectCharacterId, claim.ClaimantCharacterId),
                    claim.ClaimId))
            {
                throw new SimulationValidationException(
                    $"Claimant '{claim.ClaimantCharacterId}' has multiple active succession claims to '{claim.SubjectCharacterId}'.");
            }
        }
    }

    private void AddClaimHistory(
        IReadOnlyList<SuccessionClaimHistoryAggregate> source)
    {
        foreach (SuccessionClaimHistoryAggregate aggregate in source)
        {
            if (aggregate.ContractVersion
                    != CharacterSuccessionContractVersions.ClaimHistory
                || aggregate.FoldedWithdrawnCount <= 0
                || !aggregate.EarliestDate.IsValid
                || !aggregate.LatestDate.IsValid
                || aggregate.EarliestDate.CompareTo(aggregate.LatestDate) > 0
                || aggregate.LatestDate.CompareTo(calendar.Date) > 0)
            {
                throw new SimulationValidationException(
                    "Succession-claim history aggregate is malformed.");
            }

            AuthoritativeCharacterProfile subject = RequireCharacter(
                aggregate.SubjectCharacterId,
                "Succession-claim history subject");
            if (subject.BirthDate.CompareTo(aggregate.EarliestDate) > 0)
            {
                throw new SimulationValidationException(
                    "Succession-claim history predates its subject's birth.");
            }

            try
            {
                if (aggregate.TotalFoldedCount <= 0)
                {
                    throw new SimulationValidationException(
                        "Succession-claim history aggregate must retain at least one folded record.");
                }
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Succession-claim history aggregate exceeds Int64 capacity: {exception.Message}");
            }

            if (!claimHistory.TryAdd(
                    aggregate.SubjectCharacterId,
                    Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate succession-claim history for '{aggregate.SubjectCharacterId}'.");
            }
        }
    }

    private void AddSupports(IReadOnlyList<SuccessionSupportState> source)
    {
        foreach (SuccessionSupportState support in source)
        {
            ValidateSupport(support);
            if (!supports.TryAdd(support.SupportId, Clone(support)))
            {
                throw new SimulationValidationException(
                    $"Duplicate succession support '{support.SupportId}'.");
            }

            if (support.Status == SuccessionSupportStatus.Active
                && !activeSupportByPair.TryAdd(
                    (support.SubjectId, support.SupporterId),
                    support.SupportId))
            {
                throw new SimulationValidationException(
                    $"Supporter '{support.SupporterId}' has multiple active succession supports for '{support.SubjectId}'.");
            }
        }
    }

    private void AddSupportHistory(
        IReadOnlyList<SuccessionSupportHistoryAggregate> source)
    {
        foreach (SuccessionSupportHistoryAggregate aggregate in source)
        {
            if (aggregate.ContractVersion
                    != CharacterSuccessionContractVersions.SupportHistory
                || aggregate.FoldedReplacedCount < 0
                || aggregate.FoldedWithdrawnCount < 0
                || !aggregate.EarliestDate.IsValid
                || !aggregate.LatestDate.IsValid
                || aggregate.EarliestDate.CompareTo(aggregate.LatestDate) > 0
                || aggregate.LatestDate.CompareTo(calendar.Date) > 0)
            {
                throw new SimulationValidationException(
                    "Succession-support history aggregate is malformed.");
            }

            AuthoritativeCharacterProfile subject = RequireCharacter(
                aggregate.SubjectId,
                "Succession-support history subject");
            if (subject.BirthDate.CompareTo(aggregate.EarliestDate) > 0)
            {
                throw new SimulationValidationException(
                    "Succession-support history predates its subject's birth.");
            }

            try
            {
                if (aggregate.TotalFoldedCount <= 0)
                {
                    throw new SimulationValidationException(
                        "Succession-support history aggregate must retain at least one folded record.");
                }
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Succession-support history aggregate exceeds Int64 capacity: {exception.Message}");
            }

            if (!supportHistory.TryAdd(aggregate.SubjectId, Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate succession-support history for '{aggregate.SubjectId}'.");
            }
        }
    }

    private void ValidateClaim(SuccessionClaimState claim)
    {
        if (claim.ContractVersion != CharacterSuccessionContractVersions.ClaimState
            || !claim.ClaimId.IsValid
            || !Enum.IsDefined(claim.Origin)
            || claim.Origin != SuccessionClaimOrigin.PersonalAssertion
            || !claim.AssertedDate.IsValid
            || claim.AssertedTurnIndex < 0
            || !claim.SourceCommandId.IsValid
            || !claim.SourceEventId.IsValid
            || !Enum.IsDefined(claim.Status))
        {
            throw new SimulationValidationException(
                "Succession-claim record contains an invalid version, ID, date, turn, origin, or status.");
        }

        AuthoritativeCharacterProfile subject = RequireCharacter(
            claim.SubjectCharacterId,
            "Succession-claim subject");
        AuthoritativeCharacterProfile claimant = RequireCharacter(
            claim.ClaimantCharacterId,
            "Succession-claim claimant");
        if (claim.SubjectCharacterId == claim.ClaimantCharacterId
            || subject.BirthDate.CompareTo(claim.AssertedDate) > 0
            || claimant.BirthDate.CompareTo(claim.AssertedDate) > 0
            || claim.AssertedDate.CompareTo(calendar.Date) > 0
            || claim.AssertedTurnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Succession claim '{claim.ClaimId}' has invalid participants or assertion time.");
        }

        EntityId expectedEventId = CharacterSuccessionIds.DeriveClaimActionEventId(
            claim.AssertedDate,
            claim.SourceCommandId);
        if (claim.SourceEventId != expectedEventId
            || claim.ClaimId != CharacterSuccessionIds.DeriveClaimId(
                claim.SourceEventId,
                claim.SubjectCharacterId,
                claim.ClaimantCharacterId))
        {
            throw new SimulationValidationException(
                $"Succession claim '{claim.ClaimId}' has invalid stable identity evidence.");
        }

        if (claim.Status == SuccessionClaimStatus.Active)
        {
            if (claim.WithdrawalDate is not null
                || claim.WithdrawalTurnIndex is not null
                || claim.WithdrawalCommandId is not null
                || claim.WithdrawalEventId is not null)
            {
                throw new SimulationValidationException(
                    $"Active succession claim '{claim.ClaimId}' contains withdrawal evidence.");
            }

            return;
        }

        if (claim.WithdrawalDate is not CampaignDate withdrawalDate
            || claim.WithdrawalTurnIndex is not long withdrawalTurnIndex
            || claim.WithdrawalCommandId is not EntityId withdrawalCommandId
            || claim.WithdrawalEventId is not EntityId withdrawalEventId
            || !withdrawalDate.IsValid
            || withdrawalTurnIndex < claim.AssertedTurnIndex
            || withdrawalDate.CompareTo(claim.AssertedDate) < 0
            || withdrawalDate.CompareTo(calendar.Date) > 0
            || withdrawalTurnIndex > calendar.TurnIndex
            || !withdrawalCommandId.IsValid
            || !withdrawalEventId.IsValid
            || withdrawalCommandId == claim.SourceCommandId
            || withdrawalEventId == claim.SourceEventId
            || withdrawalEventId != CharacterSuccessionIds.DeriveClaimActionEventId(
                withdrawalDate,
                withdrawalCommandId))
        {
            throw new SimulationValidationException(
                $"Withdrawn succession claim '{claim.ClaimId}' has incomplete or invalid withdrawal evidence.");
        }
    }

    private void ValidateSupport(SuccessionSupportState support)
    {
        if (support.ContractVersion != CharacterSuccessionContractVersions.SupportState
            || !support.SupportId.IsValid
            || !support.DeclaredDate.IsValid
            || support.DeclaredTurnIndex < 0
            || !support.SourceCommandId.IsValid
            || !support.SourceEventId.IsValid
            || !Enum.IsDefined(support.Status))
        {
            throw new SimulationValidationException(
                "Succession-support record contains an invalid version, ID, date, turn, or status.");
        }

        AuthoritativeCharacterProfile subject = RequireCharacter(
            support.SubjectId,
            "Succession-support subject");
        AuthoritativeCharacterProfile supporter = RequireCharacter(
            support.SupporterId,
            "Succession-support supporter");
        AuthoritativeCharacterProfile candidate = RequireCharacter(
            support.SupportedCandidateId,
            "Succession-support supported candidate");
        if (support.SubjectId == support.SupporterId
            || support.SubjectId == support.SupportedCandidateId
            || support.SupporterId == support.SupportedCandidateId
            || subject.BirthDate.CompareTo(support.DeclaredDate) > 0
            || supporter.BirthDate.CompareTo(support.DeclaredDate) > 0
            || candidate.BirthDate.CompareTo(support.DeclaredDate) > 0
            || support.DeclaredDate.CompareTo(calendar.Date) > 0
            || support.DeclaredTurnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Succession support '{support.SupportId}' has invalid participants or declaration time.");
        }

        EntityId expectedEventId = CharacterSuccessionIds.DeriveSupportActionEventId(
            support.DeclaredDate,
            support.SourceCommandId);
        if (support.SourceEventId != expectedEventId
            || support.SupportId != CharacterSuccessionIds.DeriveSupportId(
                support.SourceEventId,
                support.SubjectId,
                support.SupporterId,
                support.SupportedCandidateId))
        {
            throw new SimulationValidationException(
                $"Succession support '{support.SupportId}' has invalid stable identity evidence.");
        }

        if (support.Status == SuccessionSupportStatus.Active)
        {
            if (support.ResolutionDate is not null
                || support.ResolutionTurnIndex is not null
                || support.ResolutionCommandId is not null
                || support.ResolutionEventId is not null)
            {
                throw new SimulationValidationException(
                    $"Active succession support '{support.SupportId}' contains resolution evidence.");
            }

            return;
        }

        if (support.ResolutionDate is not CampaignDate resolutionDate
            || support.ResolutionTurnIndex is not long resolutionTurnIndex
            || support.ResolutionCommandId is not EntityId resolutionCommandId
            || support.ResolutionEventId is not EntityId resolutionEventId
            || !resolutionDate.IsValid
            || resolutionTurnIndex < support.DeclaredTurnIndex
            || resolutionDate.CompareTo(support.DeclaredDate) < 0
            || resolutionDate.CompareTo(calendar.Date) > 0
            || resolutionTurnIndex > calendar.TurnIndex
            || !resolutionCommandId.IsValid
            || !resolutionEventId.IsValid
            || resolutionCommandId == support.SourceCommandId
            || resolutionEventId == support.SourceEventId
            || resolutionEventId != CharacterSuccessionIds.DeriveSupportActionEventId(
                resolutionDate,
                resolutionCommandId))
        {
            throw new SimulationValidationException(
                $"Terminal succession support '{support.SupportId}' has incomplete or invalid resolution evidence.");
        }
    }

    private void ValidateDesignation(HeirDesignationState designation)
    {
        if (designation.ContractVersion != CharacterSuccessionContractVersions.State
            || !designation.DesignationId.IsValid
            || !designation.EstablishedDate.IsValid
            || designation.EstablishedTurnIndex < 0
            || !designation.SourceCommandId.IsValid
            || !designation.SourceEventId.IsValid
            || !Enum.IsDefined(designation.Status))
        {
            throw new SimulationValidationException(
                "Heir-designation record contains an invalid version, ID, date, turn, or status.");
        }

        AuthoritativeCharacterProfile designator = RequireCharacter(
            designation.DesignatorCharacterId,
            "Heir-designation designator");
        AuthoritativeCharacterProfile heir = RequireCharacter(
            designation.HeirCharacterId,
            "Heir-designation heir");
        if (designation.DesignatorCharacterId == designation.HeirCharacterId
            || designator.BirthDate.CompareTo(designation.EstablishedDate) > 0
            || heir.BirthDate.CompareTo(designation.EstablishedDate) > 0
            || designation.EstablishedDate.CompareTo(calendar.Date) > 0
            || designation.EstablishedTurnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                $"Heir designation '{designation.DesignationId}' has invalid participants or establishment time.");
        }

        EntityId expectedEventId = CharacterSuccessionIds.DeriveActionEventId(
            designation.EstablishedDate,
            designation.SourceCommandId);
        if (designation.SourceEventId != expectedEventId
            || designation.DesignationId != CharacterSuccessionIds.DeriveDesignationId(
                designation.SourceEventId,
                designation.DesignatorCharacterId,
                designation.HeirCharacterId))
        {
            throw new SimulationValidationException(
                $"Heir designation '{designation.DesignationId}' has invalid stable identity evidence.");
        }

        if (designation.Status == HeirDesignationStatus.Active)
        {
            if (designation.ResolutionDate is not null
                || designation.ResolutionTurnIndex is not null
                || designation.ResolutionCommandId is not null
                || designation.ResolutionEventId is not null)
            {
                throw new SimulationValidationException(
                    $"Active heir designation '{designation.DesignationId}' contains terminal evidence.");
            }

            return;
        }

        if (designation.ResolutionDate is not CampaignDate resolutionDate
            || designation.ResolutionTurnIndex is not long resolutionTurnIndex
            || designation.ResolutionCommandId is not EntityId resolutionCommandId
            || designation.ResolutionEventId is not EntityId resolutionEventId
            || !resolutionDate.IsValid
            || resolutionTurnIndex < designation.EstablishedTurnIndex
            || resolutionDate.CompareTo(designation.EstablishedDate) < 0
            || resolutionDate.CompareTo(calendar.Date) > 0
            || resolutionTurnIndex > calendar.TurnIndex
            || !resolutionCommandId.IsValid
            || !resolutionEventId.IsValid
            || resolutionCommandId == designation.SourceCommandId
            || resolutionEventId == designation.SourceEventId
            || resolutionEventId != CharacterSuccessionIds.DeriveActionEventId(
                resolutionDate,
                resolutionCommandId))
        {
            throw new SimulationValidationException(
                $"Terminal heir designation '{designation.DesignationId}' has incomplete or invalid resolution evidence.");
        }
    }

    private void ValidateRetentionBounds()
    {
        HeirDesignationState[] allRecords = designations.Values.ToArray();
        HeirDesignationState[] allTerminalRecords = allRecords
            .Where(item => item.Status != HeirDesignationStatus.Active)
            .ToArray();
        if (allRecords.Select(item => item.SourceEventId).Distinct().Count()
                != allRecords.Length
            || allRecords.Select(item => item.SourceCommandId).Distinct().Count()
                != allRecords.Length
            || allTerminalRecords.Select(item => item.ResolutionEventId!.Value)
                .Distinct().Count() != allTerminalRecords.Length
            || allTerminalRecords.Select(item => item.ResolutionCommandId!.Value)
                .Distinct().Count() != allTerminalRecords.Length)
        {
            throw new SimulationValidationException(
                "Heir-designation state has duplicate global lifecycle event roles.");
        }

        EntityId[] designationLifecycleIdentities = allRecords
            .SelectMany(item => new EntityId?[]
            {
                item.SourceCommandId,
                item.SourceEventId,
                item.ResolutionCommandId,
                item.ResolutionEventId,
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        EntityId[] claimLifecycleIdentities = claims.Values
            .SelectMany(item => new EntityId?[]
            {
                item.SourceCommandId,
                item.SourceEventId,
                item.WithdrawalCommandId,
                item.WithdrawalEventId,
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
        SuccessionSupportState[] allSupports = supports.Values.ToArray();
        SuccessionSupportState[] allTerminalSupports = allSupports
            .Where(item => item.Status != SuccessionSupportStatus.Active)
            .ToArray();
        if (allSupports.Select(item => item.SourceEventId).Distinct().Count()
                != allSupports.Length
            || allSupports.Select(item => item.SourceCommandId).Distinct().Count()
                != allSupports.Length
            || allTerminalSupports.Select(item => item.ResolutionEventId!.Value)
                .Distinct().Count() != allTerminalSupports.Length
            || allTerminalSupports.Select(item => item.ResolutionCommandId!.Value)
                .Distinct().Count() != allTerminalSupports.Length)
        {
            throw new SimulationValidationException(
                "Succession-support state has duplicate global lifecycle event roles.");
        }

        var supportIdentityRoles = allSupports.SelectMany(item => new[]
        {
            new
            {
                Identity = item.SourceCommandId,
                Support = item,
                IsSource = true,
                IsCommand = true,
            },
            new
            {
                Identity = item.SourceEventId,
                Support = item,
                IsSource = true,
                IsCommand = false,
            },
            item.ResolutionCommandId is EntityId resolutionCommandId
                ? new
                {
                    Identity = resolutionCommandId,
                    Support = item,
                    IsSource = false,
                    IsCommand = true,
                }
                : null,
            item.ResolutionEventId is EntityId resolutionEventId
                ? new
                {
                    Identity = resolutionEventId,
                    Support = item,
                    IsSource = false,
                    IsCommand = false,
                }
                : null,
        }).Where(item => item is not null).Select(item => item!);
        foreach (var identityGroup in supportIdentityRoles.GroupBy(
                     item => item.Identity))
        {
            var roles = identityGroup.ToArray();
            if (roles.Length == 1)
            {
                continue;
            }

            var source = roles.SingleOrDefault(item => item.IsSource);
            var resolution = roles.SingleOrDefault(item => !item.IsSource);
            if (roles.Length != 2
                || source is null
                || resolution is null
                || source.IsCommand != resolution.IsCommand
                || !IsExactSupportSuccessor(
                    resolution.Support,
                    source.Support))
            {
                throw new SimulationValidationException(
                    $"Succession-support lifecycle identity '{identityGroup.Key}' has an invalid shared role.");
            }
        }

        EntityId[] supportLifecycleIdentities = allSupports
            .SelectMany(item => new EntityId?[]
            {
                item.SourceCommandId,
                item.SourceEventId,
                item.ResolutionCommandId,
                item.ResolutionEventId,
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        if (claimLifecycleIdentities.Distinct().Count()
                != claimLifecycleIdentities.Length
            || claimLifecycleIdentities.Intersect(
                designationLifecycleIdentities).Any()
            || supportLifecycleIdentities.Intersect(
                designationLifecycleIdentities).Any()
            || supportLifecycleIdentities.Intersect(
                claimLifecycleIdentities).Any())
        {
            throw new SimulationValidationException(
                "Character-succession state has duplicate or cross-workflow lifecycle identities.");
        }

        foreach (HeirDesignationState terminal in allTerminalRecords)
        {
            HeirDesignationState[] linkedSources = allRecords.Where(candidate =>
                    candidate.DesignationId != terminal.DesignationId
                    && (candidate.SourceEventId == terminal.ResolutionEventId
                        || candidate.SourceCommandId == terminal.ResolutionCommandId))
                .ToArray();
            if (terminal.Status == HeirDesignationStatus.Replaced
                ? linkedSources.Length != 1
                    || !IsExactSuccessor(terminal, linkedSources[0])
                : linkedSources.Length != 0)
            {
                throw new SimulationValidationException(
                    $"Terminal heir designation '{terminal.DesignationId}' has an invalid global resolution-to-source event role.");
            }
        }

        foreach (IGrouping<EntityId, HeirDesignationState> group in designations.Values
                     .GroupBy(item => item.DesignatorCharacterId))
        {
            if (group.Count(item => item.Status == HeirDesignationStatus.Active) > 1
                || group.Count(item => item.Status != HeirDesignationStatus.Active)
                    > CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{group.Key}' exceeds heir-designation retention bounds.");
            }

            HeirDesignationState[] records = group.ToArray();
            Dictionary<EntityId, EntityId> successorByPredecessor = [];
            HashSet<EntityId> claimedSuccessors = [];
            foreach (HeirDesignationState replaced in records.Where(
                         item => item.Status == HeirDesignationStatus.Replaced))
            {
                HeirDesignationState[] exactSuccessors = records
                    .Where(candidate => IsExactSuccessor(replaced, candidate))
                    .ToArray();
                if (exactSuccessors.Length != 1
                    || !claimedSuccessors.Add(exactSuccessors[0].DesignationId))
                {
                    throw new SimulationValidationException(
                        $"Replaced heir designation '{replaced.DesignationId}' must have one unshared same-event successor record.");
                }

                successorByPredecessor.Add(
                    replaced.DesignationId,
                    exactSuccessors[0].DesignationId);
            }

            foreach (EntityId start in successorByPredecessor.Keys)
            {
                HashSet<EntityId> path = [];
                EntityId current = start;
                while (successorByPredecessor.TryGetValue(current, out EntityId successor))
                {
                    if (!path.Add(current))
                    {
                        throw new SimulationValidationException(
                            $"Character '{group.Key}' has a cyclic heir-designation lifecycle.");
                    }

                    current = successor;
                }
            }
        }

        foreach ((EntityId designator, HeirDesignationHistoryAggregate aggregate) in history)
        {
            HeirDesignationState[] retainedTerminal = designations.Values
                .Where(item => item.DesignatorCharacterId == designator
                    && item.Status != HeirDesignationStatus.Active)
                .OrderBy(item => item.ResolutionTurnIndex)
                .ThenBy(item => item.ResolutionDate)
                .ThenBy(item => item.DesignationId)
                .ToArray();
            if (retainedTerminal.Length
                    != CharacterSuccessionLimits.RecentTerminalDesignationsPerCharacter
                || aggregate.LatestDate.CompareTo(retainedTerminal[0].ResolutionDate!.Value) > 0)
            {
                throw new SimulationValidationException(
                    $"Heir-designation history for '{designator}' is inconsistent with retained terminal records.");
            }
        }

        foreach (IGrouping<EntityId, SuccessionClaimState> group in claims.Values
                     .GroupBy(item => item.SubjectCharacterId))
        {
            if (group.Count(item => item.Status == SuccessionClaimStatus.Active)
                    > CharacterSuccessionLimits.MaximumActiveClaimsPerSubject
                || group.Count(item => item.Status == SuccessionClaimStatus.Withdrawn)
                    > CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject)
            {
                throw new SimulationValidationException(
                    $"Subject '{group.Key}' exceeds succession-claim bounds.");
            }
        }

        foreach (IGrouping<EntityId, SuccessionClaimState> group in claims.Values
                     .Where(item => item.Status == SuccessionClaimStatus.Active)
                     .GroupBy(item => item.ClaimantCharacterId))
        {
            if (group.Count()
                > CharacterSuccessionLimits.MaximumActiveClaimsPerClaimant)
            {
                throw new SimulationValidationException(
                    $"Claimant '{group.Key}' exceeds active succession-claim capacity.");
            }
        }

        foreach ((EntityId subject, SuccessionClaimHistoryAggregate aggregate)
                 in claimHistory)
        {
            SuccessionClaimState[] retainedWithdrawn = claims.Values
                .Where(item => item.SubjectCharacterId == subject
                    && item.Status == SuccessionClaimStatus.Withdrawn)
                .OrderBy(item => item.WithdrawalTurnIndex)
                .ThenBy(item => item.WithdrawalDate)
                .ThenBy(item => item.ClaimId)
                .ToArray();
            if (retainedWithdrawn.Length
                    != CharacterSuccessionLimits.RecentWithdrawnClaimsPerSubject
                || aggregate.LatestDate.CompareTo(
                    retainedWithdrawn[0].WithdrawalDate!.Value) > 0)
            {
                throw new SimulationValidationException(
                    $"Succession-claim history for '{subject}' is inconsistent with retained withdrawn records.");
            }
        }

        foreach (SuccessionSupportState terminal in allTerminalSupports)
        {
            SuccessionSupportState[] linkedSources = allSupports.Where(candidate =>
                    candidate.SupportId != terminal.SupportId
                    && (candidate.SourceEventId == terminal.ResolutionEventId
                        || candidate.SourceCommandId == terminal.ResolutionCommandId))
                .ToArray();
            if (terminal.Status == SuccessionSupportStatus.Replaced
                ? linkedSources.Length != 1
                    || !IsExactSupportSuccessor(terminal, linkedSources[0])
                : linkedSources.Length != 0)
            {
                throw new SimulationValidationException(
                    $"Terminal succession support '{terminal.SupportId}' has an invalid global resolution-to-source event role.");
            }
        }

        foreach (IGrouping<EntityId, SuccessionSupportState> group in supports.Values
                     .GroupBy(item => item.SubjectId))
        {
            if (group.Count(item => item.Status == SuccessionSupportStatus.Active)
                    > CharacterSuccessionLimits.MaximumActiveSupportsPerSubject
                || group.Count(item => item.Status != SuccessionSupportStatus.Active)
                    > CharacterSuccessionLimits.RecentTerminalSupportsPerSubject)
            {
                throw new SimulationValidationException(
                    $"Subject '{group.Key}' exceeds succession-support bounds.");
            }

            foreach (IGrouping<EntityId, SuccessionSupportState> pair in group
                         .GroupBy(item => item.SupporterId))
            {
                if (pair.Count(item => item.Status == SuccessionSupportStatus.Active) > 1)
                {
                    throw new SimulationValidationException(
                        $"Supporter '{pair.Key}' has multiple active succession supports for '{group.Key}'.");
                }

                SuccessionSupportState[] records = pair.ToArray();
                Dictionary<EntityId, EntityId> successorByPredecessor = [];
                HashSet<EntityId> claimedSuccessors = [];
                foreach (SuccessionSupportState replaced in records.Where(
                             item => item.Status == SuccessionSupportStatus.Replaced))
                {
                    SuccessionSupportState[] exactSuccessors = records
                        .Where(candidate => IsExactSupportSuccessor(
                            replaced,
                            candidate))
                        .ToArray();
                    if (exactSuccessors.Length != 1
                        || !claimedSuccessors.Add(exactSuccessors[0].SupportId))
                    {
                        throw new SimulationValidationException(
                            $"Replaced succession support '{replaced.SupportId}' must have one unshared same-event successor record.");
                    }

                    successorByPredecessor.Add(
                        replaced.SupportId,
                        exactSuccessors[0].SupportId);
                }

                foreach (EntityId start in successorByPredecessor.Keys)
                {
                    HashSet<EntityId> path = [];
                    EntityId current = start;
                    while (successorByPredecessor.TryGetValue(
                               current,
                               out EntityId successor))
                    {
                        if (!path.Add(current))
                        {
                            throw new SimulationValidationException(
                                $"Succession-support pair '{group.Key}'/'{pair.Key}' has a cyclic lifecycle.");
                        }

                        current = successor;
                    }
                }
            }
        }

        foreach (IGrouping<EntityId, SuccessionSupportState> group in supports.Values
                     .Where(item => item.Status == SuccessionSupportStatus.Active)
                     .GroupBy(item => item.SupporterId))
        {
            if (group.Count()
                > CharacterSuccessionLimits.MaximumActiveSupportsPerSupporter)
            {
                throw new SimulationValidationException(
                    $"Supporter '{group.Key}' exceeds active succession-support capacity.");
            }
        }

        foreach ((EntityId subject, SuccessionSupportHistoryAggregate aggregate)
                 in supportHistory)
        {
            SuccessionSupportState[] retainedTerminal = supports.Values
                .Where(item => item.SubjectId == subject
                    && item.Status != SuccessionSupportStatus.Active)
                .OrderBy(item => item.ResolutionTurnIndex)
                .ThenBy(item => item.ResolutionDate)
                .ThenBy(item => item.SupportId)
                .ToArray();
            if (retainedTerminal.Length
                    != CharacterSuccessionLimits.RecentTerminalSupportsPerSubject
                || aggregate.LatestDate.CompareTo(
                    retainedTerminal[0].ResolutionDate!.Value) > 0)
            {
                throw new SimulationValidationException(
                    $"Succession-support history for '{subject}' is inconsistent with retained terminal records.");
            }
        }
    }

    private static bool IsExactSuccessor(
        HeirDesignationState predecessor,
        HeirDesignationState candidate) =>
        predecessor.Status == HeirDesignationStatus.Replaced
        && candidate.DesignationId != predecessor.DesignationId
        && candidate.DesignatorCharacterId == predecessor.DesignatorCharacterId
        && candidate.EstablishedDate == predecessor.ResolutionDate
        && candidate.EstablishedTurnIndex == predecessor.ResolutionTurnIndex
        && candidate.SourceCommandId == predecessor.ResolutionCommandId
        && candidate.SourceEventId == predecessor.ResolutionEventId;

    private static bool IsExactSupportSuccessor(
        SuccessionSupportState predecessor,
        SuccessionSupportState candidate) =>
        predecessor.Status == SuccessionSupportStatus.Replaced
        && candidate.SupportId != predecessor.SupportId
        && candidate.SubjectId == predecessor.SubjectId
        && candidate.SupporterId == predecessor.SupporterId
        && candidate.DeclaredDate == predecessor.ResolutionDate
        && candidate.DeclaredTurnIndex == predecessor.ResolutionTurnIndex
        && candidate.SourceCommandId == predecessor.ResolutionCommandId
        && candidate.SourceEventId == predecessor.ResolutionEventId;

    private HeirDesignationState? GetStoredCurrent(EntityId designatorCharacterId) =>
        activeByDesignator.TryGetValue(designatorCharacterId, out EntityId designationId)
            ? designations[designationId]
            : null;

    private SuccessionClaimState? GetStoredActiveClaim(
        EntityId subjectCharacterId,
        EntityId claimantCharacterId) =>
        activeClaimByPair.TryGetValue(
            (subjectCharacterId, claimantCharacterId),
            out EntityId claimId)
                ? claims[claimId]
                : null;

    private SuccessionSupportState? GetStoredCurrentSupport(
        EntityId subjectId,
        EntityId supporterId) =>
        activeSupportByPair.TryGetValue(
            (subjectId, supporterId),
            out EntityId supportId)
                ? supports[supportId]
                : null;

    private bool HasRetainedLifecycleIdentity(EntityId identity) =>
        designations.Values.Any(item =>
            item.SourceCommandId == identity
            || item.SourceEventId == identity
            || item.ResolutionCommandId == identity
            || item.ResolutionEventId == identity)
        || claims.Values.Any(item =>
            item.SourceCommandId == identity
            || item.SourceEventId == identity
            || item.WithdrawalCommandId == identity
            || item.WithdrawalEventId == identity)
        || supports.Values.Any(item =>
            item.SourceCommandId == identity
            || item.SourceEventId == identity
            || item.ResolutionCommandId == identity
            || item.ResolutionEventId == identity);

    private AuthoritativeCharacterProfile RequireCharacter(EntityId characterId, string label)
    {
        if (!characterId.IsValid
            || !characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? profile))
        {
            throw new SimulationValidationException($"{label} '{characterId}' does not exist.");
        }

        return profile;
    }

    private void ReplaceFrom(CharacterSuccessionWorldState candidate)
    {
        designations.Clear();
        foreach ((EntityId id, HeirDesignationState designation) in candidate.designations)
        {
            designations.Add(id, Clone(designation));
        }

        activeByDesignator.Clear();
        foreach ((EntityId designator, EntityId designationId) in candidate.activeByDesignator)
        {
            activeByDesignator.Add(designator, designationId);
        }

        history.Clear();
        foreach ((EntityId id, HeirDesignationHistoryAggregate aggregate) in candidate.history)
        {
            history.Add(id, Clone(aggregate));
        }


        claims.Clear();
        foreach ((EntityId id, SuccessionClaimState claim) in candidate.claims)
        {
            claims.Add(id, Clone(claim));
        }

        activeClaimByPair.Clear();
        foreach ((var pair, EntityId claimId) in candidate.activeClaimByPair)
        {
            activeClaimByPair.Add(pair, claimId);
        }

        claimHistory.Clear();
        foreach ((EntityId id, SuccessionClaimHistoryAggregate aggregate)
                 in candidate.claimHistory)
        {
            claimHistory.Add(id, Clone(aggregate));
        }

        supports.Clear();
        foreach ((EntityId id, SuccessionSupportState support) in candidate.supports)
        {
            supports.Add(id, Clone(support));
        }

        activeSupportByPair.Clear();
        foreach ((var pair, EntityId supportId) in candidate.activeSupportByPair)
        {
            activeSupportByPair.Add(pair, supportId);
        }

        supportHistory.Clear();
        foreach ((EntityId id, SuccessionSupportHistoryAggregate aggregate)
                 in candidate.supportHistory)
        {
            supportHistory.Add(id, Clone(aggregate));
        }

        calendar = candidate.calendar;
    }

    private static HeirDesignationState CreateActiveDesignation(
        EntityId designatorCharacterId,
        EntityId heirCharacterId,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => new(
            CharacterSuccessionContractVersions.State,
            CharacterSuccessionIds.DeriveDesignationId(
                eventId,
                designatorCharacterId,
                heirCharacterId),
            designatorCharacterId,
            heirCharacterId,
            resolutionDate,
            resolutionTurnIndex,
            commandId,
            eventId,
            HeirDesignationStatus.Active,
            null,
            null,
            null,
            null);

    private static HeirDesignationState ResolveDesignation(
        HeirDesignationState current,
        HeirDesignationStatus status,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => current with
        {
            Status = status,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = resolutionTurnIndex,
            ResolutionCommandId = commandId,
            ResolutionEventId = eventId,
        };

    private static SuccessionClaimState CreateActiveClaim(
        EntityId subjectCharacterId,
        EntityId claimantCharacterId,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => new(
            CharacterSuccessionContractVersions.ClaimState,
            CharacterSuccessionIds.DeriveClaimId(
                eventId,
                subjectCharacterId,
                claimantCharacterId),
            subjectCharacterId,
            claimantCharacterId,
            SuccessionClaimOrigin.PersonalAssertion,
            resolutionDate,
            resolutionTurnIndex,
            commandId,
            eventId,
            SuccessionClaimStatus.Active,
            null,
            null,
            null,
            null);

    private static SuccessionClaimState WithdrawClaim(
        SuccessionClaimState current,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => current with
        {
            Status = SuccessionClaimStatus.Withdrawn,
            WithdrawalDate = resolutionDate,
            WithdrawalTurnIndex = resolutionTurnIndex,
            WithdrawalCommandId = commandId,
            WithdrawalEventId = eventId,
        };

    private static SuccessionSupportState CreateActiveSupport(
        EntityId subjectId,
        EntityId supporterId,
        EntityId supportedCandidateId,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => new(
            CharacterSuccessionContractVersions.SupportState,
            CharacterSuccessionIds.DeriveSupportId(
                eventId,
                subjectId,
                supporterId,
                supportedCandidateId),
            subjectId,
            supporterId,
            supportedCandidateId,
            resolutionDate,
            resolutionTurnIndex,
            commandId,
            eventId,
            SuccessionSupportStatus.Active,
            null,
            null,
            null,
            null);

    private static SuccessionSupportState ResolveSupport(
        SuccessionSupportState current,
        SuccessionSupportStatus status,
        CampaignDate resolutionDate,
        long resolutionTurnIndex,
        EntityId commandId,
        EntityId eventId) => current with
        {
            Status = status,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = resolutionTurnIndex,
            ResolutionCommandId = commandId,
            ResolutionEventId = eventId,
        };

    private static EntityId GetOutcomeDesignator(ICharacterSuccessionActionOutcome outcome) =>
        outcome switch
        {
            HeirDesignatedOutcome value => value.CurrentDesignation.DesignatorCharacterId,
            HeirDesignationReplacedOutcome value =>
                value.CurrentDesignation.DesignatorCharacterId,
            HeirDesignationRevokedOutcome value =>
                value.PreviousDesignation.DesignatorCharacterId,
            _ => throw new SimulationValidationException(
                "Unsupported character-succession outcome type."),
        };

    private static ICharacterSuccessionAction Clone(ICharacterSuccessionAction value) =>
        value switch
        {
            DesignateHeirAction action => action with { },
            RevokeHeirDesignationAction action => action with { },
            _ => throw new SimulationValidationException(
                "Unsupported character-succession action type."),
        };

    private static ICharacterSuccessionActionOutcome Clone(
        ICharacterSuccessionActionOutcome value) => value switch
        {
            HeirDesignatedOutcome outcome => outcome with
            {
                CurrentDesignation = Clone(outcome.CurrentDesignation),
            },
            HeirDesignationReplacedOutcome outcome => outcome with
            {
                PreviousDesignation = Clone(outcome.PreviousDesignation),
                CurrentDesignation = Clone(outcome.CurrentDesignation),
            },
            HeirDesignationRevokedOutcome outcome => outcome with
            {
                PreviousDesignation = Clone(outcome.PreviousDesignation),
            },
            _ => throw new SimulationValidationException(
                "Unsupported character-succession outcome type."),
        };

    private static ICharacterSuccessionClaimAction Clone(
        ICharacterSuccessionClaimAction value) => value switch
        {
            AssertSuccessionClaimAction action => action with { },
            WithdrawSuccessionClaimAction action => action with { },
            _ => throw new SimulationValidationException(
                "Unsupported succession-claim action type."),
        };

    private static ICharacterSuccessionClaimActionOutcome Clone(
        ICharacterSuccessionClaimActionOutcome value) => value switch
        {
            SuccessionClaimAssertedOutcome outcome => outcome with
            {
                CurrentClaim = Clone(outcome.CurrentClaim),
            },
            SuccessionClaimWithdrawnOutcome outcome => outcome with
            {
                PreviousClaim = Clone(outcome.PreviousClaim),
            },
            _ => throw new SimulationValidationException(
                "Unsupported succession-claim outcome type."),
        };

    private static ICharacterSuccessionSupportAction Clone(
        ICharacterSuccessionSupportAction value) => value switch
        {
            DeclareSuccessionSupportAction action => action with { },
            WithdrawSuccessionSupportAction action => action with { },
            _ => throw new SimulationValidationException(
                "Unsupported succession-support action type."),
        };

    private static ICharacterSuccessionSupportActionOutcome Clone(
        ICharacterSuccessionSupportActionOutcome value) => value switch
        {
            SuccessionSupportDeclaredOutcome outcome => outcome with
            {
                CurrentSupport = Clone(outcome.CurrentSupport),
            },
            SuccessionSupportReplacedOutcome outcome => outcome with
            {
                PreviousSupport = Clone(outcome.PreviousSupport),
                CurrentSupport = Clone(outcome.CurrentSupport),
            },
            SuccessionSupportWithdrawnOutcome outcome => outcome with
            {
                PreviousSupport = Clone(outcome.PreviousSupport),
            },
            _ => throw new SimulationValidationException(
                "Unsupported succession-support outcome type."),
        };

    private static HeirDesignationState Clone(HeirDesignationState value) => value with { };

    private static HeirDesignationHistoryAggregate Clone(
        HeirDesignationHistoryAggregate value) => value with { };

    private static SuccessionClaimState Clone(SuccessionClaimState value) => value with { };

    private static SuccessionClaimHistoryAggregate Clone(
        SuccessionClaimHistoryAggregate value) => value with { };

    private static SuccessionSupportState Clone(
        SuccessionSupportState value) => value with { };

    private static SuccessionSupportHistoryAggregate Clone(
        SuccessionSupportHistoryAggregate value) => value with { };

    private static CampaignDate Earlier(CampaignDate current, CampaignDate candidate) =>
        candidate.CompareTo(current) < 0 ? candidate : current;

    private static CampaignDate Later(CampaignDate current, CampaignDate candidate) =>
        candidate.CompareTo(current) > 0 ? candidate : current;

    private static void ValidateCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-succession campaign calendar is invalid.");
        }
    }

    private static void ValidateSnapshotShape(CharacterSuccessionWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterSuccessionContractVersions.Snapshot
            || snapshot.Designations is null
            || snapshot.History is null
            || snapshot.Claims is null
            || snapshot.ClaimHistory is null
            || snapshot.Supports is null
            || snapshot.SupportHistory is null
            || snapshot.Designations.Any(item => item is null)
            || snapshot.History.Any(item => item is null)
            || snapshot.Claims.Any(item => item is null)
            || snapshot.ClaimHistory.Any(item => item is null)
            || snapshot.Supports.Any(item => item is null)
            || snapshot.SupportHistory.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Character-succession snapshot has an invalid version or null collection/record.");
        }
    }

    private static void ValidateId(EntityId value, string label)
    {
        if (!value.IsValid)
        {
            throw new SimulationValidationException($"{label} is invalid.");
        }
    }

    private static void ThrowIfInvalid(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count > 0)
        {
            throw new SimulationValidationException(
                string.Join("; ", issues.Select(issue => issue.Message)));
        }
    }
}

internal sealed record CharacterSuccessionWorldUpdatePlan(
    CharacterSuccessionWorldState Candidate);
