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
        history.Values.Select(Clone).ToArray());

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

        if (designations.Values.Any(item =>
                item.SourceCommandId == commandId
                || item.ResolutionCommandId == commandId
                || item.SourceEventId == eventId
                || item.ResolutionEventId == eventId))
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

    private HeirDesignationState? GetStoredCurrent(EntityId designatorCharacterId) =>
        activeByDesignator.TryGetValue(designatorCharacterId, out EntityId designationId)
            ? designations[designationId]
            : null;

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

    private static HeirDesignationState Clone(HeirDesignationState value) => value with { };

    private static HeirDesignationHistoryAggregate Clone(
        HeirDesignationHistoryAggregate value) => value with { };

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
            || snapshot.Designations.Any(item => item is null)
            || snapshot.History.Any(item => item is null))
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
