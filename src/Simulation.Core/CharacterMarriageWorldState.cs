using System.Diagnostics.CodeAnalysis;

namespace Simulation.Core;

public sealed class CharacterMarriageWorldState : IAuthoritativeCharacterMarriageWorldQuery
{
    private const MarriageProhibitedKinship KnownKinshipFlags =
        MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings;

    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly CampaignDate snapshotDate;
    private readonly long snapshotTurnIndex;
    private readonly SortedDictionary<EntityId, MarriagePracticeState> practices = [];
    private readonly SortedDictionary<EntityId, MarriageProposalState> proposals = [];
    private readonly SortedDictionary<EntityId, PoliticalBetrothalState> betrothals = [];
    private readonly SortedDictionary<EntityId, MarriageUnionState> unions = [];
    private readonly SortedDictionary<EntityId, RomanceRouteState> romanceRoutes = [];
    private readonly SortedDictionary<EntityId, CharacterMarriageHistoryAggregate> history = [];

    public CharacterMarriageWorldState(
        CharacterMarriageWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException("Character-marriage snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException(
                "Authoritative character query cannot be null.");
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-marriage snapshot calendar is invalid.");
        }

        snapshotDate = calendar.Date;
        snapshotTurnIndex = calendar.TurnIndex;
        ValidateSnapshotShape(snapshot);
        AddPractices(snapshot.Practices);
        AddProposals(snapshot.Proposals);
        AddBetrothals(snapshot.Betrothals);
        AddUnions(snapshot.Unions);
        AddRomanceRoutes(snapshot.RomanceRoutes);
        AddHistory(snapshot.History);
        ValidateProposalSemantics();
        ValidateBetrothalSemantics();
        ValidateUnionSemantics();
        ValidateRomanceRouteSemantics();
        ValidateCausalOwnership();
        ValidateBoundsAndActiveConflicts();
    }

    public IReadOnlyList<MarriagePracticeState> Practices =>
        practices.Values.Select(Clone).ToArray();

    public IReadOnlyList<MarriageProposalState> Proposals =>
        proposals.Values.Select(Clone).ToArray();

    public IReadOnlyList<PoliticalBetrothalState> Betrothals =>
        betrothals.Values.Select(Clone).ToArray();

    public IReadOnlyList<MarriageUnionState> Unions =>
        unions.Values.Select(Clone).ToArray();

    public IReadOnlyList<RomanceRouteState> RomanceRoutes =>
        romanceRoutes.Values.Select(Clone).ToArray();

    public IReadOnlyList<CharacterMarriageHistoryAggregate> History =>
        history.Values.Select(Clone).ToArray();

    public bool TryGetPractice(
        EntityId practiceId,
        [NotNullWhen(true)] out MarriagePracticeState? practice) =>
        TryGet(practices, practiceId, Clone, out practice);

    public bool TryGetProposal(
        EntityId proposalId,
        [NotNullWhen(true)] out MarriageProposalState? proposal) =>
        TryGet(proposals, proposalId, Clone, out proposal);

    public bool TryGetBetrothal(
        EntityId betrothalId,
        [NotNullWhen(true)] out PoliticalBetrothalState? betrothal) =>
        TryGet(betrothals, betrothalId, Clone, out betrothal);

    public bool TryGetUnion(
        EntityId unionId,
        [NotNullWhen(true)] out MarriageUnionState? union) =>
        TryGet(unions, unionId, Clone, out union);

    public bool TryGetRomanceRoute(
        EntityId routeId,
        [NotNullWhen(true)] out RomanceRouteState? route) =>
        TryGet(romanceRoutes, routeId, Clone, out route);

    public bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CharacterMarriageHistoryAggregate? aggregate) =>
        TryGet(history, characterId, Clone, out aggregate);

    public IReadOnlyList<MarriageProposalState> GetProposalsInvolving(EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Marriage-proposal query character");
        return proposals.Values
            .Where(item => Involves(item.ProposerCharacterId, item.RecipientCharacterId, characterId))
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<PoliticalBetrothalState> GetBetrothalsInvolving(EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Political-betrothal query character");
        return betrothals.Values
            .Where(item => Involves(item.FirstCharacterId, item.SecondCharacterId, characterId))
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<MarriageUnionState> GetUnionsInvolving(EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Marriage-union query character");
        return unions.Values
            .Where(item => Involves(item.FirstCharacterId, item.SecondCharacterId, characterId))
            .Select(Clone)
            .ToArray();
    }

    public IReadOnlyList<RomanceRouteState> GetRomanceRoutesInvolving(EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Romance-route query character");
        return romanceRoutes.Values
            .Where(item => Involves(item.FirstCharacterId, item.SecondCharacterId, characterId))
            .Select(Clone)
            .ToArray();
    }

    public MarriageEligibilityResult EvaluateEligibility(
        MarriageEligibilityRequest request,
        CampaignDate date)
    {
        List<MarriageEligibilityReason> issues = [];
        if (request is null
            || request.ContractVersion != CharacterMarriageContractVersions.Eligibility
            || !Enum.IsDefined(request.Category))
        {
            AddIssue(issues, MarriageEligibilityReason.InvalidParticipant);
            return Result(issues);
        }

        if (!date.IsValid)
        {
            AddIssue(issues, MarriageEligibilityReason.InvalidDate);
        }

        MarriagePracticeState? practice = null;
        if (!request.PracticeId.IsValid
            || !practices.TryGetValue(request.PracticeId, out practice))
        {
            AddIssue(issues, MarriageEligibilityReason.UnknownPractice);
        }

        AuthoritativeCharacterProfile? first = GetEligibilityCharacter(
            request.FirstCharacterId,
            date,
            issues);
        AuthoritativeCharacterProfile? second = GetEligibilityCharacter(
            request.SecondCharacterId,
            date,
            issues);
        if (request.FirstCharacterId.IsValid
            && request.FirstCharacterId == request.SecondCharacterId)
        {
            AddIssue(issues, MarriageEligibilityReason.SameParticipant);
        }

        bool requiresForm = request.Category is MarriageEligibilityCategory.VoluntaryLegalUnion
            or MarriageEligibilityCategory.PoliticalBetrothal;
        if (requiresForm && request.ProposedForm is not MarriageUnionForm)
        {
            AddIssue(issues, MarriageEligibilityReason.UnsupportedUnionForm);
        }
        else if (request.Category == MarriageEligibilityCategory.VoluntaryRomance
            && request.ProposedForm is not null)
        {
            AddIssue(issues, MarriageEligibilityReason.UnsupportedUnionForm);
        }
        else if (request.ProposedForm is MarriageUnionForm form && !Enum.IsDefined(form))
        {
            AddIssue(issues, MarriageEligibilityReason.UnsupportedUnionForm);
        }

        if (request.ProposedForm is MarriageUnionForm requestedForm)
        {
            if (!HasValidConcubinagePrincipal(
                    requestedForm,
                    request.ConcubinagePrincipalCharacterId,
                    request.FirstCharacterId,
                    request.SecondCharacterId))
            {
                AddIssue(issues, MarriageEligibilityReason.InvalidConcubinagePrincipal);
            }
        }
        else if (request.ConcubinagePrincipalCharacterId is not null)
        {
            AddIssue(issues, MarriageEligibilityReason.InvalidConcubinagePrincipal);
        }

        if (practice is not null && first is not null && second is not null && date.IsValid)
        {
            AddKinshipIssues(practice, first, second, issues);
            switch (request.Category)
            {
                case MarriageEligibilityCategory.VoluntaryLegalUnion:
                    AddVoluntaryConsentIssues(first, practice.MinimumLegalUnionAge, date, issues);
                    AddVoluntaryConsentIssues(second, practice.MinimumLegalUnionAge, date, issues);
                    AddWidowIssue(practice, first.CharacterId, issues);
                    AddWidowIssue(practice, second.CharacterId, issues);
                    if (request.ProposedForm is MarriageUnionForm legalForm)
                    {
                        AddActiveLegalRelationshipLimitIssue(
                            first.CharacterId,
                            second.CharacterId,
                            issues);
                        AddUnionLimitIssues(
                            practice,
                            legalForm,
                            request.ConcubinagePrincipalCharacterId,
                            first.CharacterId,
                            second.CharacterId,
                            issues);
                    }

                    if (HasActiveUnionPair(first.CharacterId, second.CharacterId)
                        || HasActiveBetrothalPair(first.CharacterId, second.CharacterId))
                    {
                        AddIssue(issues, MarriageEligibilityReason.DuplicateActiveRelationship);
                    }

                    break;
                case MarriageEligibilityCategory.PoliticalBetrothal:
                    AddAliveIssue(first, issues);
                    AddAliveIssue(second, issues);
                    AddActiveLegalRelationshipLimitIssue(
                        first.CharacterId,
                        second.CharacterId,
                        issues);
                    bool underLegalAge = CalculateAge(first.BirthDate, date)
                            < practice.MinimumLegalUnionAge
                        || CalculateAge(second.BirthDate, date)
                            < practice.MinimumLegalUnionAge;
                    if (underLegalAge && !practice.AllowsPoliticalBetrothalBeforeLegalAge)
                    {
                        AddIssue(issues, MarriageEligibilityReason.PoliticalBetrothalDisabled);
                    }

                    if (HasActiveUnionPair(first.CharacterId, second.CharacterId)
                        || HasActiveBetrothalPair(first.CharacterId, second.CharacterId))
                    {
                        AddIssue(issues, MarriageEligibilityReason.DuplicateActiveRelationship);
                    }

                    break;
                case MarriageEligibilityCategory.VoluntaryRomance:
                    AddVoluntaryConsentIssues(first, practice.MinimumRomanceAge, date, issues);
                    AddVoluntaryConsentIssues(second, practice.MinimumRomanceAge, date, issues);
                    if (HasActiveRomancePair(first.CharacterId, second.CharacterId))
                    {
                        AddIssue(issues, MarriageEligibilityReason.DuplicateActiveRelationship);
                    }

                    break;
                case MarriageEligibilityCategory.CoercivePoliticalAction:
                    int coerciveInitiatorMinimumAge = request.ProposedForm is null
                        ? CharacterMarriageLimits.MinimumAdultAge
                        : practice.MinimumLegalUnionAge;
                    AddVoluntaryConsentIssues(
                        first,
                        coerciveInitiatorMinimumAge,
                        date,
                        issues);
                    AddAliveIssue(second, issues);
                    if (request.ProposedForm is not null
                        && CalculateAge(second.BirthDate, date)
                            < practice.MinimumLegalUnionAge)
                    {
                        AddIssue(issues, MarriageEligibilityReason.BelowMinimumAge);
                    }

                    if (request.ProposedForm is MarriageUnionForm coerciveForm)
                    {
                        AddActiveLegalRelationshipLimitIssue(
                            first.CharacterId,
                            second.CharacterId,
                            issues);
                        AddUnionLimitIssues(
                            practice,
                            coerciveForm,
                            request.ConcubinagePrincipalCharacterId,
                            first.CharacterId,
                            second.CharacterId,
                            issues);
                        if (HasActiveUnionPair(first.CharacterId, second.CharacterId)
                            || HasActiveBetrothalPair(first.CharacterId, second.CharacterId))
                        {
                            AddIssue(
                                issues,
                                MarriageEligibilityReason.DuplicateActiveRelationship);
                        }
                    }

                    break;
            }
        }

        return Result(issues);
    }

    public CharacterMarriageWorldSnapshot CaptureSnapshot() => new(
        CharacterMarriageContractVersions.Snapshot,
        practices.Values.Select(Clone).ToArray(),
        proposals.Values.Select(Clone).ToArray(),
        betrothals.Values.Select(Clone).ToArray(),
        unions.Values.Select(Clone).ToArray(),
        romanceRoutes.Values.Select(Clone).ToArray(),
        history.Values.Select(Clone).ToArray());

    private static void ValidateSnapshotShape(CharacterMarriageWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CharacterMarriageContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported character-marriage snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Practices is null
            || snapshot.Proposals is null
            || snapshot.Betrothals is null
            || snapshot.Unions is null
            || snapshot.RomanceRoutes is null
            || snapshot.History is null
            || snapshot.Practices.Any(item => item is null)
            || snapshot.Proposals.Any(item => item is null)
            || snapshot.Betrothals.Any(item => item is null)
            || snapshot.Unions.Any(item => item is null)
            || snapshot.RomanceRoutes.Any(item => item is null)
            || snapshot.History.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Character-marriage snapshot collections and entries cannot be null.");
        }
    }

    private void AddPractices(IReadOnlyList<MarriagePracticeState> source)
    {
        foreach (MarriagePracticeState practice in source)
        {
            RequireVersion(
                practice.ContractVersion,
                CharacterMarriageContractVersions.Practice,
                "Marriage practice",
                practice.PracticeId);
            RequireNamespacedId(practice.PracticeId, "marriage_practice:", "Marriage practice ID");
            if (practice.MinimumLegalUnionAge is < CharacterMarriageLimits.MinimumAdultAge
                    or > CharacterMarriageLimits.MaximumConfiguredMinimumAge
                || practice.MinimumRomanceAge is < CharacterMarriageLimits.MinimumAdultAge
                    or > CharacterMarriageLimits.MaximumConfiguredMinimumAge
                || practice.MaximumActivePrincipalSpousesPerCharacter is < 0
                    or > CharacterMarriageLimits.MaximumPrincipalSpouseLimit
                || practice.MaximumActiveConcubinageUnionsPerPrincipal is < 0
                    or > CharacterMarriageLimits.MaximumConcubinageLimit
                || practice.MaximumActiveConcubinageUnionsPerPartner is < 0
                    or > CharacterMarriageLimits.MaximumConcubinageLimit
                || (practice.ProhibitedKinship & ~KnownKinshipFlags) != 0)
            {
                throw new SimulationValidationException(
                    $"Marriage practice '{practice.PracticeId}' has invalid limits or kinship flags.");
            }

            if (!practices.TryAdd(practice.PracticeId, Clone(practice)))
            {
                throw new SimulationValidationException(
                    $"Duplicate marriage practice '{practice.PracticeId}'.");
            }
        }
    }

    private void AddProposals(IReadOnlyList<MarriageProposalState> source)
    {
        foreach (MarriageProposalState proposal in source)
        {
            RequireVersion(
                proposal.ContractVersion,
                CharacterMarriageContractVersions.State,
                "Marriage proposal",
                proposal.ProposalId);
            RequireNamespacedId(proposal.ProposalId, "marriage_proposal:", "Marriage proposal ID");
            RequireNamespacedId(proposal.SourceCommandId, "command:", "Marriage proposal source command ID");
            RequireParticipantPair(
                proposal.ProposerCharacterId,
                proposal.RecipientCharacterId,
                $"Marriage proposal '{proposal.ProposalId}'");
            RequirePractice(proposal.PracticeId, $"Marriage proposal '{proposal.ProposalId}'");
            RequireEnum(proposal.Kind, $"Marriage proposal '{proposal.ProposalId}' kind");
            RequireEnum(proposal.Basis, $"Marriage proposal '{proposal.ProposalId}' basis");
            RequireEnum(proposal.ProposedForm, $"Marriage proposal '{proposal.ProposalId}' form");
            RequireEnum(proposal.ConsentKind, $"Marriage proposal '{proposal.ProposalId}' consent kind");
            RequireEnum(proposal.Status, $"Marriage proposal '{proposal.ProposalId}' status");
            RequireConcubinagePrincipal(
                proposal.ProposedForm,
                proposal.ConcubinagePrincipalCharacterId,
                proposal.ProposerCharacterId,
                proposal.RecipientCharacterId,
                $"Marriage proposal '{proposal.ProposalId}'");
            ValidateStart(proposal.CreatedDate, proposal.CreatedTurnIndex, $"Marriage proposal '{proposal.ProposalId}'");
            ValidateTerminalFields(
                proposal.Status == MarriageProposalStatus.Active,
                proposal.CreatedDate,
                proposal.CreatedTurnIndex,
                proposal.ResolutionDate,
                proposal.ResolutionTurnIndex,
                proposal.ResolutionCommandId,
                $"Marriage proposal '{proposal.ProposalId}'");
            if (!proposals.TryAdd(proposal.ProposalId, Clone(proposal)))
            {
                throw new SimulationValidationException(
                    $"Duplicate marriage proposal '{proposal.ProposalId}'.");
            }
        }
    }

    private void AddBetrothals(IReadOnlyList<PoliticalBetrothalState> source)
    {
        foreach (PoliticalBetrothalState betrothal in source)
        {
            RequireVersion(
                betrothal.ContractVersion,
                CharacterMarriageContractVersions.State,
                "Political betrothal",
                betrothal.BetrothalId);
            RequireNamespacedId(
                betrothal.BetrothalId,
                "political_betrothal:",
                "Political betrothal ID");
            RequireNamespacedId(
                betrothal.SourceProposalId,
                "marriage_proposal:",
                "Political betrothal source proposal ID");
            RequireCanonicalPair(
                betrothal.FirstCharacterId,
                betrothal.SecondCharacterId,
                $"Political betrothal '{betrothal.BetrothalId}'");
            RequirePractice(betrothal.PracticeId, $"Political betrothal '{betrothal.BetrothalId}'");
            RequireEnum(betrothal.IntendedForm, $"Political betrothal '{betrothal.BetrothalId}' form");
            RequireEnum(betrothal.Status, $"Political betrothal '{betrothal.BetrothalId}' status");
            if (betrothal.FulfillmentUnionId is EntityId fulfillmentUnionId)
            {
                RequireNamespacedId(
                    fulfillmentUnionId,
                    "marriage_union:",
                    $"Political betrothal '{betrothal.BetrothalId}' fulfillment union ID");
            }

            RequireConcubinagePrincipal(
                betrothal.IntendedForm,
                betrothal.ConcubinagePrincipalCharacterId,
                betrothal.FirstCharacterId,
                betrothal.SecondCharacterId,
                $"Political betrothal '{betrothal.BetrothalId}'");
            ValidateStart(betrothal.StartDate, betrothal.StartTurnIndex, $"Political betrothal '{betrothal.BetrothalId}'");
            ValidateTerminalFields(
                betrothal.Status == PoliticalBetrothalStatus.Active,
                betrothal.StartDate,
                betrothal.StartTurnIndex,
                betrothal.ResolutionDate,
                betrothal.ResolutionTurnIndex,
                betrothal.ResolutionCommandId,
                $"Political betrothal '{betrothal.BetrothalId}'");
            if (!betrothals.TryAdd(betrothal.BetrothalId, Clone(betrothal)))
            {
                throw new SimulationValidationException(
                    $"Duplicate political betrothal '{betrothal.BetrothalId}'.");
            }
        }
    }

    private void AddUnions(IReadOnlyList<MarriageUnionState> source)
    {
        foreach (MarriageUnionState union in source)
        {
            RequireVersion(
                union.ContractVersion,
                CharacterMarriageContractVersions.State,
                "Marriage union",
                union.UnionId);
            RequireNamespacedId(union.UnionId, "marriage_union:", "Marriage union ID");
            RequireNamespacedId(
                union.SourceProposalId,
                "marriage_proposal:",
                "Marriage union source proposal ID");
            RequireCanonicalPair(
                union.FirstCharacterId,
                union.SecondCharacterId,
                $"Marriage union '{union.UnionId}'");
            RequirePractice(union.PracticeId, $"Marriage union '{union.UnionId}'");
            RequireEnum(union.Form, $"Marriage union '{union.UnionId}' form");
            RequireEnum(union.Basis, $"Marriage union '{union.UnionId}' basis");
            RequireEnum(union.ConsentKind, $"Marriage union '{union.UnionId}' consent kind");
            RequireEnum(union.Status, $"Marriage union '{union.UnionId}' status");
            if (union.EndReason is MarriageUnionEndReason endReason)
            {
                RequireEnum(endReason, $"Marriage union '{union.UnionId}' end reason");
            }

            RequireConcubinagePrincipal(
                union.Form,
                union.ConcubinagePrincipalCharacterId,
                union.FirstCharacterId,
                union.SecondCharacterId,
                $"Marriage union '{union.UnionId}'");
            ValidateStart(union.StartDate, union.StartTurnIndex, $"Marriage union '{union.UnionId}'");
            ValidateUnionTerminalFields(union);
            if (!unions.TryAdd(union.UnionId, Clone(union)))
            {
                throw new SimulationValidationException($"Duplicate marriage union '{union.UnionId}'.");
            }
        }
    }

    private void AddRomanceRoutes(IReadOnlyList<RomanceRouteState> source)
    {
        foreach (RomanceRouteState route in source)
        {
            RequireVersion(
                route.ContractVersion,
                CharacterMarriageContractVersions.State,
                "Romance route",
                route.RouteId);
            RequireNamespacedId(route.RouteId, "romance_route:", "Romance route ID");
            RequireNamespacedId(route.SourceCommandId, "command:", "Romance route source command ID");
            RequireCanonicalPair(
                route.FirstCharacterId,
                route.SecondCharacterId,
                $"Romance route '{route.RouteId}'");
            RequirePractice(route.PracticeId, $"Romance route '{route.RouteId}'");
            RequireEnum(route.Status, $"Romance route '{route.RouteId}' status");
            if (route.ProgressLevel is < 0 or > CharacterMarriageLimits.MaximumRomanceProgressLevel)
            {
                throw new SimulationValidationException(
                    $"Romance route '{route.RouteId}' has invalid progress level {route.ProgressLevel}.");
            }

            ValidateStart(route.StartDate, route.StartTurnIndex, $"Romance route '{route.RouteId}'");
            ValidateTerminalFields(
                route.Status == RomanceRouteStatus.Active,
                route.StartDate,
                route.StartTurnIndex,
                route.ResolutionDate,
                route.ResolutionTurnIndex,
                route.ResolutionCommandId,
                $"Romance route '{route.RouteId}'");
            if (!romanceRoutes.TryAdd(route.RouteId, Clone(route)))
            {
                throw new SimulationValidationException($"Duplicate romance route '{route.RouteId}'.");
            }
        }
    }

    private void AddHistory(IReadOnlyList<CharacterMarriageHistoryAggregate> source)
    {
        foreach (CharacterMarriageHistoryAggregate aggregate in source)
        {
            RequireVersion(
                aggregate.ContractVersion,
                CharacterMarriageContractVersions.State,
                "Character-marriage history",
                aggregate.CharacterId);
            AuthoritativeCharacterProfile owner = RequireBornCharacter(
                aggregate.CharacterId,
                snapshotDate,
                $"Character-marriage history '{aggregate.CharacterId}' owner");
            long total;
            try
            {
                total = checked(
                    aggregate.FoldedProposalCount
                    + aggregate.FoldedBetrothalCount
                    + aggregate.FoldedUnionCount
                    + aggregate.FoldedRomanceRouteCount);
            }
            catch (OverflowException)
            {
                throw new SimulationValidationException(
                    $"Character-marriage history '{aggregate.CharacterId}' counts overflow.");
            }

            if (aggregate.FoldedProposalCount < 0
                || aggregate.FoldedBetrothalCount < 0
                || aggregate.FoldedUnionCount < 0
                || aggregate.FoldedRomanceRouteCount < 0
                || (total == 0 && (aggregate.EarliestDate is not null || aggregate.LatestDate is not null))
                || (total > 0 && (aggregate.EarliestDate is null || aggregate.LatestDate is null))
                || (aggregate.EarliestDate is CampaignDate earliest
                    && (!earliest.IsValid
                        || earliest.CompareTo(owner.BirthDate) < 0
                        || earliest.CompareTo(snapshotDate) > 0))
                || (aggregate.LatestDate is CampaignDate latest
                    && (!latest.IsValid
                        || latest.CompareTo(owner.BirthDate) < 0
                        || latest.CompareTo(snapshotDate) > 0))
                || (aggregate.EarliestDate is CampaignDate first
                    && aggregate.LatestDate is CampaignDate last
                    && first.CompareTo(last) > 0))
            {
                throw new SimulationValidationException(
                    $"Character-marriage history '{aggregate.CharacterId}' is inconsistent.");
            }

            if (!history.TryAdd(aggregate.CharacterId, Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate character-marriage history '{aggregate.CharacterId}'.");
            }
        }
    }

    private void ValidateProposalSemantics()
    {
        foreach (MarriageProposalState proposal in proposals.Values)
        {
            MarriagePracticeState practice = practices[proposal.PracticeId];
            AuthoritativeCharacterProfile proposer = RequireBornCharacter(
                proposal.ProposerCharacterId,
                proposal.CreatedDate,
                $"Marriage proposal '{proposal.ProposalId}' proposer");
            AuthoritativeCharacterProfile recipient = RequireBornCharacter(
                proposal.RecipientCharacterId,
                proposal.CreatedDate,
                $"Marriage proposal '{proposal.ProposalId}' recipient");
            ThrowIfProhibitedKinship(practice, proposer, recipient, $"Marriage proposal '{proposal.ProposalId}'");

            if (proposal.Kind == MarriageProposalKind.PoliticalBetrothal)
            {
                if (proposal.Basis != MarriageBasis.Political
                    || proposal.ConsentKind != MarriageConsentKind.PoliticalArrangement)
                {
                    throw new SimulationValidationException(
                        $"Political-betrothal proposal '{proposal.ProposalId}' must remain political and cannot record romance or consent.");
                }

                bool underAge = CalculateAge(proposer.BirthDate, proposal.CreatedDate)
                        < practice.MinimumLegalUnionAge
                    || CalculateAge(recipient.BirthDate, proposal.CreatedDate)
                        < practice.MinimumLegalUnionAge;
                if (underAge && !practice.AllowsPoliticalBetrothalBeforeLegalAge)
                {
                    throw new SimulationValidationException(
                        $"Political-betrothal proposal '{proposal.ProposalId}' violates its practice's minor-betrothal rule.");
                }
            }
            else
            {
                RequireMinimumAge(proposer, proposal.CreatedDate, practice.MinimumLegalUnionAge, $"Marriage proposal '{proposal.ProposalId}' proposer");
                RequireMinimumAge(recipient, proposal.CreatedDate, practice.MinimumLegalUnionAge, $"Marriage proposal '{proposal.ProposalId}' recipient");
                if (proposal.Basis == MarriageBasis.Romantic
                    && proposal.ConsentKind != MarriageConsentKind.Voluntary)
                {
                    throw new SimulationValidationException(
                        $"Romantic marriage proposal '{proposal.ProposalId}' must be voluntary.");
                }

                if (proposal.ConsentKind == MarriageConsentKind.Coerced
                    && proposal.Basis != MarriageBasis.Political)
                {
                    throw new SimulationValidationException(
                        $"Coerced marriage proposal '{proposal.ProposalId}' must remain political.");
                }
            }

            if (proposal.Status == MarriageProposalStatus.Active)
            {
                RequireAlive(proposer, $"Marriage proposal '{proposal.ProposalId}' proposer");
                RequireAlive(recipient, $"Marriage proposal '{proposal.ProposalId}' recipient");
                if (proposal.Kind == MarriageProposalKind.LegalUnion
                    && proposal.ConsentKind != MarriageConsentKind.Coerced)
                {
                    RequireAbleToConsent(proposer, $"Marriage proposal '{proposal.ProposalId}' proposer");
                    RequireAbleToConsent(recipient, $"Marriage proposal '{proposal.ProposalId}' recipient");
                }
            }
        }
    }

    private void ValidateBetrothalSemantics()
    {
        HashSet<EntityId> fulfillmentUnionIds = [];
        foreach (PoliticalBetrothalState betrothal in betrothals.Values)
        {
            MarriageProposalState source = RequireSourceProposal(
                betrothal.SourceProposalId,
                $"Political betrothal '{betrothal.BetrothalId}'");
            if (source.Kind != MarriageProposalKind.PoliticalBetrothal
                || source.Status != MarriageProposalStatus.Accepted
                || source.Basis != MarriageBasis.Political
                || source.ConsentKind != MarriageConsentKind.PoliticalArrangement
                || !SamePair(
                    source.ProposerCharacterId,
                    source.RecipientCharacterId,
                    betrothal.FirstCharacterId,
                    betrothal.SecondCharacterId)
                || source.ProposedForm != betrothal.IntendedForm
                || source.ConcubinagePrincipalCharacterId != betrothal.ConcubinagePrincipalCharacterId
                || source.PracticeId != betrothal.PracticeId
                || source.ResolutionDate != betrothal.StartDate
                || source.ResolutionTurnIndex != betrothal.StartTurnIndex)
            {
                throw new SimulationValidationException(
                    $"Political betrothal '{betrothal.BetrothalId}' does not exactly match its accepted political proposal.");
            }

            MarriagePracticeState practice = practices[betrothal.PracticeId];
            AuthoritativeCharacterProfile first = RequireBornCharacter(
                betrothal.FirstCharacterId,
                betrothal.StartDate,
                $"Political betrothal '{betrothal.BetrothalId}' first participant");
            AuthoritativeCharacterProfile second = RequireBornCharacter(
                betrothal.SecondCharacterId,
                betrothal.StartDate,
                $"Political betrothal '{betrothal.BetrothalId}' second participant");
            ThrowIfProhibitedKinship(practice, first, second, $"Political betrothal '{betrothal.BetrothalId}'");
            bool underAge = CalculateAge(first.BirthDate, betrothal.StartDate)
                    < practice.MinimumLegalUnionAge
                || CalculateAge(second.BirthDate, betrothal.StartDate)
                    < practice.MinimumLegalUnionAge;
            if (underAge && !practice.AllowsPoliticalBetrothalBeforeLegalAge)
            {
                throw new SimulationValidationException(
                    $"Political betrothal '{betrothal.BetrothalId}' violates its practice's minor-betrothal rule.");
            }

            if (betrothal.Status == PoliticalBetrothalStatus.Active)
            {
                RequireAlive(first, $"Political betrothal '{betrothal.BetrothalId}' first participant");
                RequireAlive(second, $"Political betrothal '{betrothal.BetrothalId}' second participant");
            }

            if (betrothal.Status != PoliticalBetrothalStatus.Fulfilled)
            {
                if (betrothal.FulfillmentUnionId is not null)
                {
                    throw new SimulationValidationException(
                        $"Political betrothal '{betrothal.BetrothalId}' has a fulfillment union without fulfilled status.");
                }

                continue;
            }

            if (betrothal.FulfillmentUnionId is not EntityId fulfillmentUnionId
                || !unions.TryGetValue(fulfillmentUnionId, out MarriageUnionState? union)
                || !fulfillmentUnionIds.Add(fulfillmentUnionId)
                || !SamePair(
                    betrothal.FirstCharacterId,
                    betrothal.SecondCharacterId,
                    union.FirstCharacterId,
                    union.SecondCharacterId)
                || betrothal.IntendedForm != union.Form
                || betrothal.ConcubinagePrincipalCharacterId
                    != union.ConcubinagePrincipalCharacterId
                || betrothal.PracticeId != union.PracticeId
                || union.Basis != MarriageBasis.Political
                || union.ConsentKind != MarriageConsentKind.PoliticalArrangement
                || betrothal.ResolutionDate != union.StartDate
                || betrothal.ResolutionTurnIndex != union.StartTurnIndex)
            {
                throw new SimulationValidationException(
                    $"Political betrothal '{betrothal.BetrothalId}' does not identify one exact political-arrangement fulfillment union.");
            }

            MarriageProposalState unionSource = RequireSourceProposal(
                union.SourceProposalId,
                $"Political betrothal '{betrothal.BetrothalId}' fulfillment union");
            if (betrothal.ResolutionCommandId != unionSource.ResolutionCommandId)
            {
                throw new SimulationValidationException(
                    $"Political betrothal '{betrothal.BetrothalId}' fulfillment command does not match its union proposal resolution.");
            }
        }
    }

    private void ValidateUnionSemantics()
    {
        foreach (MarriageUnionState union in unions.Values)
        {
            MarriageProposalState source = RequireSourceProposal(
                union.SourceProposalId,
                $"Marriage union '{union.UnionId}'");
            if (source.Kind != MarriageProposalKind.LegalUnion
                || source.Status != MarriageProposalStatus.Accepted
                || !SamePair(
                    source.ProposerCharacterId,
                    source.RecipientCharacterId,
                    union.FirstCharacterId,
                    union.SecondCharacterId)
                || source.ProposedForm != union.Form
                || source.ConcubinagePrincipalCharacterId != union.ConcubinagePrincipalCharacterId
                || source.Basis != union.Basis
                || source.ConsentKind != union.ConsentKind
                || source.PracticeId != union.PracticeId
                || source.ResolutionDate != union.StartDate
                || source.ResolutionTurnIndex != union.StartTurnIndex)
            {
                throw new SimulationValidationException(
                    $"Marriage union '{union.UnionId}' does not exactly match its accepted legal-union proposal.");
            }

            MarriagePracticeState practice = practices[union.PracticeId];
            AuthoritativeCharacterProfile first = RequireBornCharacter(
                union.FirstCharacterId,
                union.StartDate,
                $"Marriage union '{union.UnionId}' first participant");
            AuthoritativeCharacterProfile second = RequireBornCharacter(
                union.SecondCharacterId,
                union.StartDate,
                $"Marriage union '{union.UnionId}' second participant");
            RequireMinimumAge(first, union.StartDate, practice.MinimumLegalUnionAge, $"Marriage union '{union.UnionId}' first participant");
            RequireMinimumAge(second, union.StartDate, practice.MinimumLegalUnionAge, $"Marriage union '{union.UnionId}' second participant");
            ThrowIfProhibitedKinship(practice, first, second, $"Marriage union '{union.UnionId}'");
            if (union.Basis == MarriageBasis.Romantic
                && union.ConsentKind != MarriageConsentKind.Voluntary)
            {
                throw new SimulationValidationException(
                    $"Romantic marriage union '{union.UnionId}' must be voluntary.");
            }

            if (union.ConsentKind == MarriageConsentKind.Coerced
                && union.Basis != MarriageBasis.Political)
            {
                throw new SimulationValidationException(
                    $"Coerced marriage union '{union.UnionId}' must remain political.");
            }

            if (union.Status == MarriageUnionStatus.Active)
            {
                RequireAlive(first, $"Marriage union '{union.UnionId}' first participant");
                RequireAlive(second, $"Marriage union '{union.UnionId}' second participant");
            }
            else if (union.EndReason == MarriageUnionEndReason.SpouseDied
                && first.Condition.VitalStatus != CharacterVitalStatus.Dead
                && second.Condition.VitalStatus != CharacterVitalStatus.Dead)
            {
                throw new SimulationValidationException(
                    $"Marriage union '{union.UnionId}' claims spouse death but neither participant is dead.");
            }
        }
    }

    private void ValidateRomanceRouteSemantics()
    {
        foreach (RomanceRouteState route in romanceRoutes.Values)
        {
            MarriagePracticeState practice = practices[route.PracticeId];
            AuthoritativeCharacterProfile first = RequireBornCharacter(
                route.FirstCharacterId,
                route.StartDate,
                $"Romance route '{route.RouteId}' first participant");
            AuthoritativeCharacterProfile second = RequireBornCharacter(
                route.SecondCharacterId,
                route.StartDate,
                $"Romance route '{route.RouteId}' second participant");
            RequireMinimumAge(first, route.StartDate, practice.MinimumRomanceAge, $"Romance route '{route.RouteId}' first participant");
            RequireMinimumAge(second, route.StartDate, practice.MinimumRomanceAge, $"Romance route '{route.RouteId}' second participant");
            ThrowIfProhibitedKinship(practice, first, second, $"Romance route '{route.RouteId}'");
            if (route.Status == RomanceRouteStatus.Active)
            {
                RequireAlive(first, $"Romance route '{route.RouteId}' first participant");
                RequireAlive(second, $"Romance route '{route.RouteId}' second participant");
                RequireAbleToConsent(first, $"Romance route '{route.RouteId}' first participant");
                RequireAbleToConsent(second, $"Romance route '{route.RouteId}' second participant");
            }
        }
    }

    private void ValidateCausalOwnership()
    {
        Dictionary<EntityId, int> unionOutcomes = unions.Values
            .GroupBy(item => item.SourceProposalId)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<EntityId, int> betrothalOutcomes = betrothals.Values
            .GroupBy(item => item.SourceProposalId)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (MarriageProposalState proposal in proposals.Values)
        {
            int unionCount = unionOutcomes.GetValueOrDefault(proposal.ProposalId);
            int betrothalCount = betrothalOutcomes.GetValueOrDefault(proposal.ProposalId);
            bool validOutcome = proposal.Status == MarriageProposalStatus.Accepted
                ? proposal.Kind switch
                {
                    MarriageProposalKind.LegalUnion => unionCount == 1 && betrothalCount == 0,
                    MarriageProposalKind.PoliticalBetrothal => unionCount == 0 && betrothalCount == 1,
                    _ => false,
                }
                : unionCount == 0 && betrothalCount == 0;
            if (!validOutcome)
            {
                throw new SimulationValidationException(
                    $"Marriage proposal '{proposal.ProposalId}' does not own exactly the outcome required by its status and kind.");
            }
        }

        HashSet<EntityId> creationCommands = [];
        foreach ((EntityId commandId, string label) in proposals.Values
                     .Select(item => (item.SourceCommandId, $"marriage proposal '{item.ProposalId}'"))
                     .Concat(romanceRoutes.Values.Select(item =>
                         (item.SourceCommandId, $"romance route '{item.RouteId}'"))))
        {
            if (!creationCommands.Add(commandId))
            {
                throw new SimulationValidationException(
                    $"Creation command '{commandId}' is reused by {label}.");
            }
        }

        HashSet<EntityId> coerciveCommands = proposals.Values
            .Where(item => item.ConsentKind == MarriageConsentKind.Coerced)
            .SelectMany(item => item.ResolutionCommandId is EntityId resolutionCommandId
                ? new[] { item.SourceCommandId, resolutionCommandId }
                : [item.SourceCommandId])
            .ToHashSet();
        if (romanceRoutes.Values.Any(item =>
            coerciveCommands.Contains(item.SourceCommandId)
            || (item.Status == RomanceRouteStatus.Completed
                && item.ResolutionCommandId is EntityId resolutionCommandId
                && coerciveCommands.Contains(resolutionCommandId))))
        {
            throw new SimulationValidationException(
                "A coercive proposal command cannot create or resolve positive romance-route state.");
        }
    }

    private void ValidateBoundsAndActiveConflicts()
    {
        ValidateRetainedBound(
            proposals.Values,
            item => (item.ProposerCharacterId, item.RecipientCharacterId),
            "marriage proposals");
        ValidateRetainedBound(
            betrothals.Values,
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "political betrothals");
        ValidateRetainedBound(
            unions.Values,
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "marriage unions");
        ValidateRetainedBound(
            romanceRoutes.Values,
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "romance routes");

        foreach (IGrouping<EntityId, MarriageProposalState> recipient in proposals.Values
                     .Where(item => item.Status == MarriageProposalStatus.Active)
                     .GroupBy(item => item.RecipientCharacterId))
        {
            if (recipient.Count() > CharacterMarriageLimits.ActiveProposalsPerRecipient)
            {
                throw new SimulationValidationException(
                    $"Character '{recipient.Key}' exceeds the active marriage-proposal limit of {CharacterMarriageLimits.ActiveProposalsPerRecipient}.");
            }
        }

        EnsureUniqueActivePairs(
            proposals.Values.Where(item => item.Status == MarriageProposalStatus.Active),
            item => CanonicalPair(item.ProposerCharacterId, item.RecipientCharacterId),
            "marriage proposal");
        EnsureUniqueActivePairs(
            betrothals.Values.Where(item => item.Status == PoliticalBetrothalStatus.Active),
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "political betrothal");
        EnsureUniqueActivePairs(
            unions.Values.Where(item => item.Status == MarriageUnionStatus.Active),
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "marriage union");
        EnsureUniqueActivePairs(
            romanceRoutes.Values.Where(item => item.Status == RomanceRouteStatus.Active),
            item => (item.FirstCharacterId, item.SecondCharacterId),
            "romance route");

        HashSet<(EntityId First, EntityId Second)> activeUnionPairs = unions.Values
            .Where(item => item.Status == MarriageUnionStatus.Active)
            .Select(item => (item.FirstCharacterId, item.SecondCharacterId))
            .ToHashSet();
        HashSet<(EntityId First, EntityId Second)> activeBetrothalPairs = betrothals.Values
            .Where(item => item.Status == PoliticalBetrothalStatus.Active)
            .Select(item => (item.FirstCharacterId, item.SecondCharacterId))
            .ToHashSet();
        if (activeBetrothalPairs.Overlaps(activeUnionPairs))
        {
            throw new SimulationValidationException(
                "A character pair cannot have both an active political betrothal and an active marriage union.");
        }

        if (proposals.Values
            .Where(item => item.Status == MarriageProposalStatus.Active)
            .Select(item => CanonicalPair(
                item.ProposerCharacterId,
                item.RecipientCharacterId))
            .Any(pair => activeUnionPairs.Contains(pair)
                || activeBetrothalPairs.Contains(pair)))
        {
            throw new SimulationValidationException(
                "An active marriage proposal must be terminally invalidated when its pair already has an active betrothal or union.");
        }

        foreach (EntityId characterId in characters.Profiles.Select(item => item.CharacterId))
        {
            MarriageUnionState[] active = unions.Values
                .Where(item => item.Status == MarriageUnionStatus.Active
                    && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId))
                .ToArray();
            int activeBetrothals = betrothals.Values.Count(item =>
                item.Status == PoliticalBetrothalStatus.Active
                && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId));
            if (active.Length + activeBetrothals
                > CharacterMarriageLimits.ActiveLegalRelationshipsPerCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{characterId}' exceeds the active legal-relationship limit of {CharacterMarriageLimits.ActiveLegalRelationshipsPerCharacter}.");
            }

            int principalCount = active.Count(item => item.Form == MarriageUnionForm.PrincipalSpouse);
            foreach (MarriageUnionState union in active.Where(
                         item => item.Form == MarriageUnionForm.PrincipalSpouse))
            {
                if (principalCount
                    > practices[union.PracticeId].MaximumActivePrincipalSpousesPerCharacter)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' exceeds marriage practice '{union.PracticeId}' principal-spouse limit.");
                }
            }

            int concubinagePrincipalCount = active.Count(item =>
                item.Form == MarriageUnionForm.Concubinage
                && item.ConcubinagePrincipalCharacterId == characterId);
            int concubinagePartnerCount = active.Count(item =>
                item.Form == MarriageUnionForm.Concubinage
                && item.ConcubinagePrincipalCharacterId != characterId);
            foreach (MarriageUnionState union in active.Where(
                         item => item.Form == MarriageUnionForm.Concubinage))
            {
                MarriagePracticeState practice = practices[union.PracticeId];
                int count = union.ConcubinagePrincipalCharacterId == characterId
                    ? concubinagePrincipalCount
                    : concubinagePartnerCount;
                int limit = union.ConcubinagePrincipalCharacterId == characterId
                    ? practice.MaximumActiveConcubinageUnionsPerPrincipal
                    : practice.MaximumActiveConcubinageUnionsPerPartner;
                if (count > limit)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' exceeds marriage practice '{union.PracticeId}' concubinage limit.");
                }
            }
        }
    }

    private MarriageProposalState RequireSourceProposal(EntityId id, string label)
    {
        if (!proposals.TryGetValue(id, out MarriageProposalState? proposal))
        {
            throw new SimulationValidationException($"{label} references missing source proposal '{id}'.");
        }

        return proposal;
    }

    private void RequirePractice(EntityId id, string label)
    {
        if (!id.IsValid || !practices.ContainsKey(id))
        {
            throw new SimulationValidationException($"{label} references missing marriage practice '{id}'.");
        }
    }

    private void RequireParticipantPair(EntityId first, EntityId second, string label)
    {
        _ = RequireCharacter(first, $"{label} first participant");
        _ = RequireCharacter(second, $"{label} second participant");
        if (first == second)
        {
            throw new SimulationValidationException($"{label} participants must be distinct.");
        }
    }

    private void RequireCanonicalPair(EntityId first, EntityId second, string label)
    {
        RequireParticipantPair(first, second, label);
        if (first.CompareTo(second) >= 0)
        {
            throw new SimulationValidationException(
                $"{label} participants must use strict ordinal canonical order.");
        }
    }

    private AuthoritativeCharacterProfile RequireCharacter(EntityId id, string label)
    {
        if (!id.IsValid
            || !characters.TryGetCharacterProfile(id, out AuthoritativeCharacterProfile? profile))
        {
            throw new SimulationValidationException($"{label} '{id}' does not exist.");
        }

        return profile;
    }

    private AuthoritativeCharacterProfile RequireBornCharacter(
        EntityId id,
        CampaignDate date,
        string label)
    {
        AuthoritativeCharacterProfile profile = RequireCharacter(id, label);
        if (profile.BirthDate.CompareTo(date) > 0)
        {
            throw new SimulationValidationException($"{label} '{id}' is not born by '{date}'.");
        }

        return profile;
    }

    private AuthoritativeCharacterProfile? GetEligibilityCharacter(
        EntityId id,
        CampaignDate date,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (!id.IsValid)
        {
            AddIssue(issues, MarriageEligibilityReason.InvalidParticipant);
            return null;
        }

        if (!characters.TryGetCharacterProfile(id, out AuthoritativeCharacterProfile? profile))
        {
            AddIssue(issues, MarriageEligibilityReason.UnknownParticipant);
            return null;
        }

        if (date.IsValid && profile.BirthDate.CompareTo(date) > 0)
        {
            AddIssue(issues, MarriageEligibilityReason.NotBorn);
            return null;
        }

        return profile;
    }

    private void ValidateStart(CampaignDate date, long turnIndex, string label)
    {
        if (!date.IsValid
            || date.CompareTo(snapshotDate) > 0
            || turnIndex < 0
            || turnIndex > snapshotTurnIndex)
        {
            throw new SimulationValidationException(
                $"{label} has an invalid or future start date or turn index.");
        }
    }

    private void ValidateTerminalFields(
        bool active,
        CampaignDate startDate,
        long startTurnIndex,
        CampaignDate? resolutionDate,
        long? resolutionTurnIndex,
        EntityId? resolutionCommandId,
        string label)
    {
        if (active)
        {
            if (resolutionDate is not null
                || resolutionTurnIndex is not null
                || resolutionCommandId is not null)
            {
                throw new SimulationValidationException($"{label} active status has terminal data.");
            }

            return;
        }

        if (resolutionDate is not CampaignDate terminalDate
            || !terminalDate.IsValid
            || terminalDate.CompareTo(startDate) < 0
            || terminalDate.CompareTo(snapshotDate) > 0
            || resolutionTurnIndex is not long terminalTurn
            || terminalTurn < startTurnIndex
            || terminalTurn > snapshotTurnIndex
            || resolutionCommandId is not EntityId terminalCommand)
        {
            throw new SimulationValidationException($"{label} terminal status lacks coherent terminal data.");
        }

        RequireNamespacedId(terminalCommand, "command:", $"{label} terminal command ID");
    }

    private void ValidateUnionTerminalFields(MarriageUnionState union)
    {
        if (union.Status == MarriageUnionStatus.Active)
        {
            if (union.EndDate is not null
                || union.EndTurnIndex is not null
                || union.EndCommandId is not null
                || union.EndReason is not null)
            {
                throw new SimulationValidationException(
                    $"Marriage union '{union.UnionId}' active status has terminal data.");
            }

            return;
        }

        if (union.EndDate is not CampaignDate endDate
            || !endDate.IsValid
            || endDate.CompareTo(union.StartDate) < 0
            || endDate.CompareTo(snapshotDate) > 0
            || union.EndTurnIndex is not long endTurn
            || endTurn < union.StartTurnIndex
            || endTurn > snapshotTurnIndex
            || union.EndCommandId is not EntityId endCommand
            || union.EndReason is not MarriageUnionEndReason)
        {
            throw new SimulationValidationException(
                $"Marriage union '{union.UnionId}' ended status lacks coherent terminal data.");
        }

        RequireNamespacedId(
            endCommand,
            "command:",
            $"Marriage union '{union.UnionId}' end command ID");
    }

    private static void RequireMinimumAge(
        AuthoritativeCharacterProfile profile,
        CampaignDate date,
        int minimumAge,
        string label)
    {
        if (CalculateAge(profile.BirthDate, date) < minimumAge)
        {
            throw new SimulationValidationException(
                $"{label} must be at least {minimumAge} years old.");
        }
    }

    private static void RequireAlive(AuthoritativeCharacterProfile profile, string label)
    {
        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            throw new SimulationValidationException($"{label} must be alive.");
        }
    }

    private static void RequireAbleToConsent(AuthoritativeCharacterProfile profile, string label)
    {
        if (profile.Condition.IsIncapacitated)
        {
            throw new SimulationValidationException($"{label} is incapacitated and cannot consent.");
        }

        if (profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            throw new SimulationValidationException($"{label} is in custody and cannot consent.");
        }
    }

    private void ThrowIfProhibitedKinship(
        MarriagePracticeState practice,
        AuthoritativeCharacterProfile first,
        AuthoritativeCharacterProfile second,
        string label)
    {
        if (practice.ProhibitedKinship.HasFlag(MarriageProhibitedKinship.DirectLine)
            && IsDirectLine(first.CharacterId, second.CharacterId))
        {
            throw new SimulationValidationException($"{label} violates direct-line kinship rules.");
        }

        if (practice.ProhibitedKinship.HasFlag(MarriageProhibitedKinship.Siblings)
            && first.ParentIds.Intersect(second.ParentIds).Any())
        {
            throw new SimulationValidationException($"{label} violates sibling kinship rules.");
        }
    }

    private bool IsDirectLine(EntityId first, EntityId second) =>
        IsAncestor(first, second) || IsAncestor(second, first);

    private bool IsAncestor(EntityId possibleAncestor, EntityId characterId)
    {
        HashSet<EntityId> visited = [];
        Stack<EntityId> pending = new();
        pending.Push(characterId);
        while (pending.Count > 0)
        {
            EntityId current = pending.Pop();
            if (!visited.Add(current)
                || !characters.TryGetCharacterProfile(
                    current,
                    out AuthoritativeCharacterProfile? profile))
            {
                continue;
            }

            foreach (EntityId parentId in profile.ParentIds)
            {
                if (parentId == possibleAncestor)
                {
                    return true;
                }

                pending.Push(parentId);
            }
        }

        return false;
    }

    private void AddKinshipIssues(
        MarriagePracticeState practice,
        AuthoritativeCharacterProfile first,
        AuthoritativeCharacterProfile second,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (practice.ProhibitedKinship.HasFlag(MarriageProhibitedKinship.DirectLine)
            && IsDirectLine(first.CharacterId, second.CharacterId))
        {
            AddIssue(issues, MarriageEligibilityReason.ProhibitedDirectLineKinship);
        }

        if (practice.ProhibitedKinship.HasFlag(MarriageProhibitedKinship.Siblings)
            && first.ParentIds.Intersect(second.ParentIds).Any())
        {
            AddIssue(issues, MarriageEligibilityReason.ProhibitedSiblingKinship);
        }
    }

    private static void AddAliveIssue(
        AuthoritativeCharacterProfile profile,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            AddIssue(issues, MarriageEligibilityReason.Dead);
        }
    }

    private static void AddVoluntaryConsentIssues(
        AuthoritativeCharacterProfile profile,
        int minimumAge,
        CampaignDate date,
        ICollection<MarriageEligibilityReason> issues)
    {
        AddAliveIssue(profile, issues);
        if (CalculateAge(profile.BirthDate, date) < minimumAge)
        {
            AddIssue(issues, MarriageEligibilityReason.BelowMinimumAge);
        }

        if (profile.Condition.IsIncapacitated)
        {
            AddIssue(issues, MarriageEligibilityReason.Incapacitated);
        }

        if (profile.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            AddIssue(issues, MarriageEligibilityReason.InCustody);
        }
    }

    private void AddWidowIssue(
        MarriagePracticeState practice,
        EntityId characterId,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (!practice.AllowsWidowRemarriage
            && unions.Values.Any(item =>
                item.Status == MarriageUnionStatus.Ended
                && item.EndReason == MarriageUnionEndReason.SpouseDied
                && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId)))
        {
            AddIssue(issues, MarriageEligibilityReason.WidowRemarriageDisabled);
        }
    }

    private void AddUnionLimitIssues(
        MarriagePracticeState practice,
        MarriageUnionForm form,
        EntityId? concubinagePrincipal,
        EntityId first,
        EntityId second,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (form == MarriageUnionForm.PrincipalSpouse)
        {
            if (ActivePrincipalCount(first) >= practice.MaximumActivePrincipalSpousesPerCharacter
                || ActivePrincipalCount(second) >= practice.MaximumActivePrincipalSpousesPerCharacter)
            {
                AddIssue(issues, MarriageEligibilityReason.ActiveUnionLimitReached);
            }

            return;
        }

        if (concubinagePrincipal is not EntityId principal)
        {
            return;
        }

        EntityId partner = principal == first ? second : first;
        int principalCount = unions.Values.Count(item =>
            item.Status == MarriageUnionStatus.Active
            && item.Form == MarriageUnionForm.Concubinage
            && item.ConcubinagePrincipalCharacterId == principal);
        int partnerCount = unions.Values.Count(item =>
            item.Status == MarriageUnionStatus.Active
            && item.Form == MarriageUnionForm.Concubinage
            && item.ConcubinagePrincipalCharacterId != partner
            && Involves(item.FirstCharacterId, item.SecondCharacterId, partner));
        if (principalCount >= practice.MaximumActiveConcubinageUnionsPerPrincipal
            || partnerCount >= practice.MaximumActiveConcubinageUnionsPerPartner)
        {
            AddIssue(issues, MarriageEligibilityReason.ActiveUnionLimitReached);
        }
    }

    private void AddActiveLegalRelationshipLimitIssue(
        EntityId first,
        EntityId second,
        ICollection<MarriageEligibilityReason> issues)
    {
        if (ActiveLegalRelationshipCount(first)
                >= CharacterMarriageLimits.ActiveLegalRelationshipsPerCharacter
            || ActiveLegalRelationshipCount(second)
                >= CharacterMarriageLimits.ActiveLegalRelationshipsPerCharacter)
        {
            AddIssue(issues, MarriageEligibilityReason.ActiveUnionLimitReached);
        }
    }

    private int ActiveLegalRelationshipCount(EntityId characterId) =>
        unions.Values.Count(item => item.Status == MarriageUnionStatus.Active
            && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId))
        + betrothals.Values.Count(item => item.Status == PoliticalBetrothalStatus.Active
            && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId));

    private int ActivePrincipalCount(EntityId characterId) => unions.Values.Count(item =>
        item.Status == MarriageUnionStatus.Active
        && item.Form == MarriageUnionForm.PrincipalSpouse
        && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId));

    private bool HasActiveUnionPair(EntityId first, EntityId second) => unions.Values.Any(item =>
        item.Status == MarriageUnionStatus.Active
        && SamePair(first, second, item.FirstCharacterId, item.SecondCharacterId));

    private bool HasActiveBetrothalPair(EntityId first, EntityId second) => betrothals.Values.Any(item =>
        item.Status == PoliticalBetrothalStatus.Active
        && SamePair(first, second, item.FirstCharacterId, item.SecondCharacterId));

    private bool HasActiveRomancePair(EntityId first, EntityId second) => romanceRoutes.Values.Any(item =>
        item.Status == RomanceRouteStatus.Active
        && SamePair(first, second, item.FirstCharacterId, item.SecondCharacterId));

    private static void ValidateRetainedBound<T>(
        IEnumerable<T> source,
        Func<T, (EntityId First, EntityId Second)> participants,
        string description)
    {
        Dictionary<EntityId, int> counts = [];
        foreach (T item in source)
        {
            (EntityId first, EntityId second) = participants(item);
            foreach (EntityId characterId in new[] { first, second }.Distinct())
            {
                int count = counts.TryGetValue(characterId, out int existing)
                    ? checked(existing + 1)
                    : 1;
                if (count > CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' exceeds the retained {description} limit of {CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter}.");
                }

                counts[characterId] = count;
            }
        }
    }

    private static void EnsureUniqueActivePairs<T>(
        IEnumerable<T> source,
        Func<T, (EntityId First, EntityId Second)> pair,
        string description)
    {
        HashSet<(EntityId First, EntityId Second)> seen = [];
        foreach (T item in source)
        {
            if (!seen.Add(pair(item)))
            {
                throw new SimulationValidationException(
                    $"Duplicate active {description} participant pair.");
            }
        }
    }

    private static bool HasValidConcubinagePrincipal(
        MarriageUnionForm form,
        EntityId? principal,
        EntityId first,
        EntityId second) => form switch
        {
            MarriageUnionForm.PrincipalSpouse => principal is null,
            MarriageUnionForm.Concubinage => principal is EntityId value
                && value.IsValid
                && (value == first || value == second),
            _ => false,
        };

    private static void RequireConcubinagePrincipal(
        MarriageUnionForm form,
        EntityId? principal,
        EntityId first,
        EntityId second,
        string label)
    {
        if (!HasValidConcubinagePrincipal(form, principal, first, second))
        {
            throw new SimulationValidationException(
                $"{label} has an invalid concubinage-principal role.");
        }
    }

    private static void RequireNamespacedId(EntityId id, string prefix, string label)
    {
        if (!id.IsValid || !id.Value.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new SimulationValidationException(
                $"{label} '{id}' must use the '{prefix}' namespace.");
        }
    }

    private static void RequireVersion(int actual, int expected, string label, EntityId id)
    {
        if (actual != expected)
        {
            throw new SimulationValidationException(
                $"{label} '{id}' has unsupported contract version {actual}.");
        }
    }

    private static void RequireEnum<T>(T value, string label)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new SimulationValidationException($"{label} is invalid.");
        }
    }

    private static int CalculateAge(CampaignDate birthDate, CampaignDate date)
    {
        int age = date.Year - birthDate.Year;
        if (date.Month < birthDate.Month
            || (date.Month == birthDate.Month && date.Day < birthDate.Day))
        {
            age--;
        }

        return age;
    }

    private static (EntityId First, EntityId Second) CanonicalPair(EntityId first, EntityId second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static bool SamePair(
        EntityId first,
        EntityId second,
        EntityId otherFirst,
        EntityId otherSecond) =>
        CanonicalPair(first, second) == CanonicalPair(otherFirst, otherSecond);

    private static bool Involves(EntityId first, EntityId second, EntityId characterId) =>
        first == characterId || second == characterId;

    private static MarriageEligibilityResult Result(
        IEnumerable<MarriageEligibilityReason> issues)
    {
        MarriageEligibilityIssue[] canonical = issues
            .Distinct()
            .Order()
            .Select(reason => new MarriageEligibilityIssue(reason))
            .ToArray();
        return new MarriageEligibilityResult(
            CharacterMarriageContractVersions.Eligibility,
            canonical.Length == 0,
            canonical);
    }

    private static void AddIssue(
        ICollection<MarriageEligibilityReason> issues,
        MarriageEligibilityReason issue)
    {
        if (!issues.Contains(issue))
        {
            issues.Add(issue);
        }
    }

    private static bool TryGet<T>(
        IReadOnlyDictionary<EntityId, T> source,
        EntityId id,
        Func<T, T> clone,
        [NotNullWhen(true)] out T? value)
        where T : class
    {
        if (source.TryGetValue(id, out T? stored))
        {
            value = clone(stored);
            return true;
        }

        value = null;
        return false;
    }

    private static MarriagePracticeState Clone(MarriagePracticeState value) => value with { };

    private static MarriageProposalState Clone(MarriageProposalState value) => value with { };

    private static PoliticalBetrothalState Clone(PoliticalBetrothalState value) => value with { };

    private static MarriageUnionState Clone(MarriageUnionState value) => value with { };

    private static RomanceRouteState Clone(RomanceRouteState value) => value with { };

    private static CharacterMarriageHistoryAggregate Clone(
        CharacterMarriageHistoryAggregate value) => value with { };
}
