using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core;

public sealed class CharacterMarriageWorldState : IAuthoritativeCharacterMarriageWorldQuery
{
    private const MarriageProhibitedKinship KnownKinshipFlags =
        MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings;

    private readonly IAuthoritativeCharacterWorldQuery characters;
    private CampaignCalendar calendar;
    private readonly SortedDictionary<EntityId, MarriagePracticeState> practices = [];
    private readonly SortedDictionary<EntityId, MarriageProposalState> proposals = [];
    private readonly SortedDictionary<EntityId, PoliticalBetrothalState> betrothals = [];
    private readonly SortedDictionary<EntityId, MarriageUnionState> unions = [];
    private readonly SortedDictionary<EntityId, RomanceInvitationState> romanceInvitations = [];
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

        this.calendar = calendar;
        ValidateSnapshotShape(snapshot);
        AddPractices(snapshot.Practices);
        AddProposals(snapshot.Proposals);
        AddBetrothals(snapshot.Betrothals);
        AddUnions(snapshot.Unions);
        AddRomanceInvitations(snapshot.Invitations ?? []);
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

    public IReadOnlyList<RomanceInvitationState> RomanceInvitations =>
        romanceInvitations.Values.Select(Clone).ToArray();

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

    public bool TryGetRomanceInvitation(
        EntityId invitationId,
        [NotNullWhen(true)] out RomanceInvitationState? invitation) =>
        TryGet(romanceInvitations, invitationId, Clone, out invitation);

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

    public IReadOnlyList<RomanceInvitationState> GetRomanceInvitationsInvolving(
        EntityId characterId)
    {
        _ = RequireCharacter(characterId, "Romance-invitation query character");
        return romanceInvitations.Values
            .Where(item => Involves(
                item.InitiatorCharacterId,
                item.RecipientCharacterId,
                characterId))
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
        history.Values.Select(Clone).ToArray(),
        romanceInvitations.Values.Select(Clone).ToArray());

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException(
                "Character-marriage campaign calendar is invalid.");
        }

        if (value.Date.CompareTo(calendar.Date) < 0 || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Character-marriage campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    public CommandValidationResult ValidateAction(
        EntityId actingCharacterId,
        CharacterMarriageActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            validateAcceptanceEligibility: true,
            issues);
        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterMarriageActionResolvedEventPayload PlanAction(
        EntityId actingCharacterId,
        CharacterMarriageActionCommandPayload payload,
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
            validateAcceptanceEligibility: false,
            issues);
        ThrowIfInvalid(issues);
        RequireNamespacedId(commandId, "command:", "Character-marriage command ID");
        RequireNamespacedId(eventId, "event:", "Character-marriage event ID");
        if (IsPositiveRomanceAction(payload.Action)
            && IsRetainedCoerciveCommand(commandId))
        {
            throw new SimulationValidationException(
                "A retained coercive proposal command cannot resolve a positive romance action.");
        }

        if (eventId != CharacterMarriageIds.DeriveActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                $"Character-marriage event ID '{eventId}' does not match command '{commandId}'.");
        }

        ICharacterMarriageAction action = Clone(payload.Action);
        ICharacterMarriageActionOutcome outcome = action switch
        {
            ProposePoliticalMarriageAction value => PlanProposal(
                actingCharacterId,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            RespondToPoliticalMarriageProposalAction value => PlanProposalResponse(
                actingCharacterId,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            WithdrawPoliticalMarriageProposalAction value => PlanProposalWithdrawal(
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            CancelPoliticalBetrothalAction value => PlanBetrothalCancellation(
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            FulfillPoliticalBetrothalAction value => PlanBetrothalFulfillment(
                actingCharacterId,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            OfferRomanceRouteAction value => PlanRomanceInvitation(
                actingCharacterId,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            RespondToRomanceInvitationAction value => PlanRomanceInvitationResponse(
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            WithdrawRomanceInvitationAction value =>
                new RomanceInvitationWithdrawnOutcome(
                    Clone(romanceInvitations[value.InvitationId])),
            AdvanceRomanceRouteAction value => PlanRomanceRouteAdvance(
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            EndRomanceRouteAction value => PlanRomanceRouteEnd(
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            ImposeCoercedUnionAction value => PlanCoercedUnion(
                actingCharacterId,
                value,
                resolutionDate,
                authoritativeTurnIndex,
                commandId),
            _ => throw new SimulationValidationException(
                "Unsupported character-marriage action type."),
        };

        return new CharacterMarriageActionResolvedEventPayload(
            actingCharacterId,
            action,
            Clone(outcome));
    }

    private static bool IsPositiveRomanceAction(ICharacterMarriageAction action) => action is
        OfferRomanceRouteAction
        or AdvanceRomanceRouteAction
        or RespondToRomanceInvitationAction
        {
            Response: RomanceInvitationResponse.Accept,
        };

    private bool IsRetainedCoerciveCommand(EntityId commandId) => proposals.Values.Any(
        proposal => proposal.ConsentKind == MarriageConsentKind.Coerced
            && (proposal.SourceCommandId == commandId
                || proposal.ResolutionCommandId == commandId));

    public void PrevalidateOutcome(
        CharacterMarriageActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId) => _ = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);

    internal CharacterMarriageWorldUpdatePlan PrepareOutcome(
        CharacterMarriageActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null || payload.Action is null || payload.Outcome is null)
        {
            throw new SimulationValidationException(
                "Character-marriage action outcome payload cannot contain null data.");
        }

        CharacterMarriageActionResolvedEventPayload expected = PlanAction(
            payload.ActingCharacterId,
            new CharacterMarriageActionCommandPayload(payload.Action),
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        if (payload.Action is ImposeCoercedUnionAction)
        {
            if (payload.RelationshipMemoryConsequence is null)
            {
                throw new SimulationValidationException(
                    "Coerced character-marriage outcome requires a harmful relationship consequence.");
            }

            expected = expected with
            {
                RelationshipMemoryConsequence = Clone(payload.RelationshipMemoryConsequence),
            };
        }
        else if (payload.RelationshipMemoryConsequence is not null)
        {
            throw new SimulationValidationException(
                "Only coerced character-marriage outcomes may contain a relationship consequence.");
        }

        string expectedJson = JsonSerializer.Serialize(expected, SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Character-marriage action outcome does not match the exact deterministic plan.");
        }

        CharacterMarriageWorldSnapshot updated = ApplyOutcomeToSnapshot(
            CaptureSnapshot(),
            payload.Outcome);
        updated = NormalizeRetention(updated);
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterMarriageWorldState candidate = new(updated, characters, candidateCalendar);
        return new CharacterMarriageWorldUpdatePlan(candidate);
    }

    internal CharacterMarriageLifecycleUpdatePlan PrepareLifecycleChange(
        IAuthoritativeCharacterWorldQuery candidateCharacters,
        EntityId changedCharacterId,
        CharacterMarriageLifecycleReason reason,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        if (candidateCharacters is null
            || !changedCharacterId.IsValid
            || !Enum.IsDefined(reason)
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex != calendar.TurnIndex
            || !commandId.IsValid
            || !candidateCharacters.TryGetCharacterProfile(
                changedCharacterId,
                out AuthoritativeCharacterProfile? changedCharacter))
        {
            throw new SimulationValidationException(
                "Character-marriage lifecycle preparation contains invalid authority, date, turn, or character data.");
        }

        bool deceased = changedCharacter.Condition.VitalStatus == CharacterVitalStatus.Dead;
        if (reason == CharacterMarriageLifecycleReason.CharacterDied != deceased)
        {
            throw new SimulationValidationException(
                "Character-marriage lifecycle reason does not match candidate vital status.");
        }

        bool consentUnavailable = deceased
            || changedCharacter.Condition.IsIncapacitated
            || changedCharacter.Condition.CustodyStatus != CharacterCustodyStatus.Free;
        MarriageProposalState[] invalidatedProposals = proposals.Values
            .Where(item => item.Status == MarriageProposalStatus.Active
                && Involves(
                    item.ProposerCharacterId,
                    item.RecipientCharacterId,
                    changedCharacterId)
                && (deceased
                    || item.Kind == MarriageProposalKind.LegalUnion
                        && item.ConsentKind != MarriageConsentKind.Coerced
                        && consentUnavailable
                    || item.Kind == MarriageProposalKind.LegalUnion
                        && item.ConsentKind == MarriageConsentKind.Coerced
                        && item.ProposerCharacterId == changedCharacterId
                        && consentUnavailable))
            .Select(item => TerminalizeProposal(
                item,
                MarriageProposalStatus.Invalidated,
                resolutionDate,
                authoritativeTurnIndex,
                commandId))
            .OrderBy(item => item.ProposalId)
            .ToArray();
        PoliticalBetrothalState[] invalidatedBetrothals = deceased
            ? betrothals.Values
                .Where(item => item.Status == PoliticalBetrothalStatus.Active
                    && Involves(
                        item.FirstCharacterId,
                        item.SecondCharacterId,
                        changedCharacterId))
                .Select(item => TerminalizeBetrothal(
                    item,
                    PoliticalBetrothalStatus.Invalidated,
                    null,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId))
                .OrderBy(item => item.BetrothalId)
                .ToArray()
            : [];
        MarriageUnionState[] endedUnions = deceased
            ? unions.Values
                .Where(item => item.Status == MarriageUnionStatus.Active
                    && Involves(
                        item.FirstCharacterId,
                        item.SecondCharacterId,
                        changedCharacterId))
                .Select(item => EndUnionForDeath(
                    item,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId))
                .OrderBy(item => item.UnionId)
                .ToArray()
            : [];
        RomanceInvitationState[] cancelledInvitations = consentUnavailable
            ? romanceInvitations.Values
                .Where(item => Involves(
                    item.InitiatorCharacterId,
                    item.RecipientCharacterId,
                    changedCharacterId))
                .Select(Clone)
                .OrderBy(item => item.InvitationId)
                .ToArray()
            : [];
        RomanceRouteState[] invalidatedRoutes = consentUnavailable
            ? romanceRoutes.Values
                .Where(item => item.Status == RomanceRouteStatus.Active
                    && Involves(
                        item.FirstCharacterId,
                        item.SecondCharacterId,
                        changedCharacterId))
                .Select(item => TerminalizeRomanceRoute(
                    item,
                    RomanceRouteStatus.Invalidated,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId))
                .OrderBy(item => item.RouteId)
                .ToArray()
            : [];

        CharacterMarriageLifecycleChangeSet changes = new(
            CharacterMarriageContractVersions.LifecycleChangeSet,
            reason,
            invalidatedProposals,
            invalidatedBetrothals,
            endedUnions,
            cancelledInvitations,
            invalidatedRoutes);
        CharacterMarriageWorldSnapshot updated = ApplyLifecycleChanges(
            CaptureSnapshot(),
            changes);
        updated = NormalizeRetention(updated);
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterMarriageWorldState candidate = new(
            updated,
            candidateCharacters,
            candidateCalendar);
        return new CharacterMarriageLifecycleUpdatePlan(
            Clone(changes),
            new CharacterMarriageWorldUpdatePlan(candidate));
    }

    internal void ApplyPrepared(CharacterMarriageWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException(
                "Prepared character-marriage update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    private ICharacterMarriageActionOutcome PlanProposal(
        EntityId actingCharacterId,
        ProposePoliticalMarriageAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            CharacterMarriageIds.DeriveProposalId(action.Kind, resolutionDate, commandId),
            action.Kind,
            MarriageBasis.Political,
            action.ProposedForm,
            MarriageConsentKind.PoliticalArrangement,
            actingCharacterId,
            action.RecipientCharacterId,
            action.ConcubinagePrincipalCharacterId,
            action.PracticeId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            MarriageProposalStatus.Active,
            null,
            null,
            null);
        return new MarriageProposalCreatedOutcome(proposal);
    }

    private ICharacterMarriageActionOutcome PlanCoercedUnion(
        EntityId actingCharacterId,
        ImposeCoercedUnionAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        EntityId proposalId = CharacterMarriageIds.DeriveProposalId(
            MarriageProposalKind.LegalUnion,
            resolutionDate,
            commandId);
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            action.ProposedForm,
            MarriageConsentKind.Coerced,
            actingCharacterId,
            action.RecipientCharacterId,
            action.ConcubinagePrincipalCharacterId,
            action.PracticeId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            MarriageProposalStatus.Accepted,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        (EntityId first, EntityId second) = CanonicalPair(
            actingCharacterId,
            action.RecipientCharacterId);
        MarriageUnionState union = CreateCoercedUnion(
            proposal,
            first,
            second,
            resolutionDate,
            authoritativeTurnIndex);
        RomanceRouteState? invalidatedRoute = romanceRoutes.Values
            .SingleOrDefault(route => route.Status == RomanceRouteStatus.Active
                && SamePair(
                    route.FirstCharacterId,
                    route.SecondCharacterId,
                    actingCharacterId,
                    action.RecipientCharacterId));
        if (invalidatedRoute is not null)
        {
            invalidatedRoute = TerminalizeRomanceRoute(
                invalidatedRoute,
                RomanceRouteStatus.Invalidated,
                resolutionDate,
                authoritativeTurnIndex,
                commandId);
        }

        return new CoercedPoliticalUnionImposedOutcome(
            proposal,
            union,
            invalidatedRoute is null ? null : Clone(invalidatedRoute));
    }

    private ICharacterMarriageActionOutcome PlanProposalResponse(
        EntityId actingCharacterId,
        RespondToPoliticalMarriageProposalAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        MarriageProposalState proposal = proposals[action.ProposalId];
        if (action.Response == MarriageProposalResponse.Refuse)
        {
            return new MarriageProposalRefusedOutcome(TerminalizeProposal(
                proposal,
                MarriageProposalStatus.Refused,
                resolutionDate,
                authoritativeTurnIndex,
                commandId));
        }

        MarriageEligibilityResult eligibility = EvaluateProposalAcceptance(
            proposal,
            resolutionDate);
        if (!eligibility.IsEligible)
        {
            return new MarriageProposalCancelledOutcome(TerminalizeProposal(
                proposal,
                MarriageProposalStatus.Cancelled,
                resolutionDate,
                authoritativeTurnIndex,
                commandId));
        }

        MarriageProposalState accepted = TerminalizeProposal(
            proposal,
            MarriageProposalStatus.Accepted,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        (EntityId first, EntityId second) = CanonicalPair(
            proposal.ProposerCharacterId,
            proposal.RecipientCharacterId);
        if (proposal.Kind == MarriageProposalKind.PoliticalBetrothal)
        {
            PoliticalBetrothalState betrothal = new(
                CharacterMarriageContractVersions.State,
                CharacterMarriageIds.DerivePoliticalBetrothalId(proposal.ProposalId),
                first,
                second,
                proposal.ProposedForm,
                proposal.ConcubinagePrincipalCharacterId,
                proposal.PracticeId,
                proposal.ProposalId,
                resolutionDate,
                authoritativeTurnIndex,
                PoliticalBetrothalStatus.Active,
                null,
                null,
                null,
                null);
            return new PoliticalBetrothalAcceptedOutcome(accepted, betrothal);
        }

        MarriageUnionState union = CreateUnion(
            accepted,
            first,
            second,
            resolutionDate,
            authoritativeTurnIndex);
        return new DirectPoliticalUnionAcceptedOutcome(accepted, union);
    }

    private ICharacterMarriageActionOutcome PlanProposalWithdrawal(
        WithdrawPoliticalMarriageProposalAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => new MarriageProposalWithdrawnOutcome(TerminalizeProposal(
            proposals[action.ProposalId],
            MarriageProposalStatus.Withdrawn,
            resolutionDate,
            authoritativeTurnIndex,
            commandId));

    private ICharacterMarriageActionOutcome PlanBetrothalCancellation(
        CancelPoliticalBetrothalAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => new PoliticalBetrothalCancelledOutcome(
            TerminalizeBetrothal(
                betrothals[action.BetrothalId],
                PoliticalBetrothalStatus.Cancelled,
                null,
                resolutionDate,
                authoritativeTurnIndex,
                commandId));

    private ICharacterMarriageActionOutcome PlanBetrothalFulfillment(
        EntityId actingCharacterId,
        FulfillPoliticalBetrothalAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        PoliticalBetrothalState betrothal = betrothals[action.BetrothalId];
        MarriageEligibilityResult eligibility = EvaluateFulfillmentEligibility(
            betrothal,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        if (!eligibility.IsEligible)
        {
            throw new SimulationValidationException(string.Join(
                "; ",
                eligibility.Issues.Select(issue => issue.Reason.ToString())));
        }

        EntityId recipient = actingCharacterId == betrothal.FirstCharacterId
            ? betrothal.SecondCharacterId
            : betrothal.FirstCharacterId;
        EntityId proposalId = CharacterMarriageIds.DeriveProposalId(
            MarriageProposalKind.LegalUnion,
            resolutionDate,
            commandId);
        MarriageProposalState fulfillmentProposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            betrothal.IntendedForm,
            MarriageConsentKind.PoliticalArrangement,
            actingCharacterId,
            recipient,
            betrothal.ConcubinagePrincipalCharacterId,
            betrothal.PracticeId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            MarriageProposalStatus.Accepted,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        MarriageUnionState union = CreateUnion(
            fulfillmentProposal,
            betrothal.FirstCharacterId,
            betrothal.SecondCharacterId,
            resolutionDate,
            authoritativeTurnIndex);
        PoliticalBetrothalState fulfilled = TerminalizeBetrothal(
            betrothal,
            PoliticalBetrothalStatus.Fulfilled,
            union.UnionId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return new PoliticalBetrothalFulfilledOutcome(
            fulfilled,
            fulfillmentProposal,
            union);
    }

    private ICharacterMarriageActionOutcome PlanRomanceInvitation(
        EntityId actingCharacterId,
        OfferRomanceRouteAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => new RomanceInvitationCreatedOutcome(new(
            CharacterMarriageContractVersions.RomanceInvitationState,
            CharacterMarriageIds.DeriveRomanceInvitationId(resolutionDate, commandId),
            actingCharacterId,
            action.RecipientCharacterId,
            action.PracticeId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId));

    private ICharacterMarriageActionOutcome PlanRomanceInvitationResponse(
        RespondToRomanceInvitationAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        RomanceInvitationState invitation = romanceInvitations[action.InvitationId];
        if (action.Response == RomanceInvitationResponse.Refuse)
        {
            return new RomanceInvitationRefusedOutcome(Clone(invitation));
        }

        MarriageEligibilityResult eligibility = EvaluateRomanceEligibility(
            invitation.InitiatorCharacterId,
            invitation.RecipientCharacterId,
            invitation.PracticeId,
            resolutionDate);
        if (!eligibility.IsEligible)
        {
            return new RomanceInvitationCancelledOutcome(Clone(invitation));
        }

        (EntityId first, EntityId second) = CanonicalPair(
            invitation.InitiatorCharacterId,
            invitation.RecipientCharacterId);
        RomanceRouteState route = new(
            CharacterMarriageContractVersions.RomanceRouteState,
            CharacterMarriageIds.DeriveRomanceRouteId(
                invitation.InvitationId,
                commandId),
            first,
            second,
            invitation.PracticeId,
            1,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            RomanceRouteStatus.Active,
            null,
            null,
            null,
            invitation.InvitationId,
            invitation.InitiatorCharacterId,
            invitation.CreatedDate,
            invitation.CreatedTurnIndex,
            invitation.SourceCommandId,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return new RomanceRouteStartedOutcome(invitation.InvitationId, route);
    }

    private ICharacterMarriageActionOutcome PlanRomanceRouteAdvance(
        AdvanceRomanceRouteAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        RomanceRouteState route = romanceRoutes[action.RouteId];
        int nextProgress = route.ProgressLevel == CharacterMarriageLimits.MaximumRomanceProgressLevel
            ? route.ProgressLevel
            : checked(route.ProgressLevel + 1);
        bool completes = nextProgress == CharacterMarriageLimits.MaximumRomanceProgressLevel;
        RomanceRouteState advanced = route with
        {
            ProgressLevel = nextProgress,
            Status = completes ? RomanceRouteStatus.Completed : RomanceRouteStatus.Active,
            ResolutionDate = completes ? resolutionDate : null,
            ResolutionTurnIndex = completes ? authoritativeTurnIndex : null,
            ResolutionCommandId = completes ? commandId : null,
            LastPositiveProgressDate = route.ContractVersion
                == CharacterMarriageContractVersions.RomanceRouteState
                    ? resolutionDate
                    : route.LastPositiveProgressDate,
            LastPositiveProgressTurnIndex = route.ContractVersion
                == CharacterMarriageContractVersions.RomanceRouteState
                    ? authoritativeTurnIndex
                    : route.LastPositiveProgressTurnIndex,
            LastPositiveProgressCommandId = route.ContractVersion
                == CharacterMarriageContractVersions.RomanceRouteState
                    ? commandId
                    : route.LastPositiveProgressCommandId,
        };
        return completes
            ? new RomanceRouteCompletedOutcome(advanced)
            : new RomanceRouteAdvancedOutcome(advanced);
    }

    private ICharacterMarriageActionOutcome PlanRomanceRouteEnd(
        EndRomanceRouteAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => new RomanceRouteEndedOutcome(
            romanceRoutes[action.RouteId] with
            {
                Status = RomanceRouteStatus.Ended,
                ResolutionDate = resolutionDate,
                ResolutionTurnIndex = authoritativeTurnIndex,
                ResolutionCommandId = commandId,
            });

    private MarriageEligibilityResult EvaluateRomanceEligibility(
        EntityId first,
        EntityId second,
        EntityId practiceId,
        CampaignDate date) => EvaluateEligibility(
            new MarriageEligibilityRequest(
                CharacterMarriageContractVersions.Eligibility,
                MarriageEligibilityCategory.VoluntaryRomance,
                first,
                second,
                practiceId,
                null,
                null),
            date);

    private MarriageEligibilityResult EvaluateProposalAcceptance(
        MarriageProposalState proposal,
        CampaignDate resolutionDate) => EvaluateEligibility(
            new MarriageEligibilityRequest(
                CharacterMarriageContractVersions.Eligibility,
                proposal.Kind == MarriageProposalKind.LegalUnion
                    ? MarriageEligibilityCategory.VoluntaryLegalUnion
                    : MarriageEligibilityCategory.PoliticalBetrothal,
                proposal.ProposerCharacterId,
                proposal.RecipientCharacterId,
                proposal.PracticeId,
                proposal.ProposedForm,
                proposal.ConcubinagePrincipalCharacterId),
            resolutionDate);

    private MarriageEligibilityResult EvaluateFulfillmentEligibility(
        PoliticalBetrothalState betrothal,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        PoliticalBetrothalState released = TerminalizeBetrothal(
            betrothal,
            PoliticalBetrothalStatus.Cancelled,
            null,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        CharacterMarriageWorldSnapshot temporary = CaptureSnapshot() with
        {
            Betrothals = betrothals.Values
                .Select(item => item.BetrothalId == betrothal.BetrothalId ? released : Clone(item))
                .ToArray(),
        };
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterMarriageWorldState replacement = new(temporary, characters, candidateCalendar);
        return replacement.EvaluateEligibility(
            new MarriageEligibilityRequest(
                CharacterMarriageContractVersions.Eligibility,
                MarriageEligibilityCategory.VoluntaryLegalUnion,
                betrothal.FirstCharacterId,
                betrothal.SecondCharacterId,
                betrothal.PracticeId,
                betrothal.IntendedForm,
                betrothal.ConcubinagePrincipalCharacterId),
            resolutionDate);
    }

    private void ValidateActionEnvelope(
        EntityId actingCharacterId,
        CharacterMarriageActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        bool validateAcceptanceEligibility,
        ICollection<ValidationIssue> issues)
    {
        AuthoritativeCharacterProfile? actor = null;
        if (!actingCharacterId.IsValid)
        {
            issues.Add(new("invalid_actor", "Character-marriage actor ID is invalid."));
        }
        else if (!characters.TryGetCharacterProfile(
                actingCharacterId,
                out actor))
        {
            issues.Add(new(
                "unknown_actor",
                $"Character-marriage actor '{actingCharacterId}' does not exist."));
        }
        else if (resolutionDate.IsValid && actor.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new("actor_not_born", "Character-marriage actor is not born."));
        }
        else if (actor.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new("actor_dead", "Character-marriage actor is dead."));
        }

        if (!resolutionDate.IsValid)
        {
            issues.Add(new(
                "invalid_resolution_date",
                "Character-marriage resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new(
                "past_resolution_date",
                "Character-marriage resolution date precedes marriage state."));
        }

        if (authoritativeTurnIndex != calendar.TurnIndex)
        {
            issues.Add(new(
                "invalid_turn_index",
                "Character-marriage action must resolve on the authoritative turn."));
        }

        if (payload?.Action is null)
        {
            issues.Add(new(
                "invalid_payload",
                "Character-marriage action payload cannot be null."));
            return;
        }

        switch (payload.Action)
        {
            case ProposePoliticalMarriageAction action:
                ValidateProposalAction(
                    actingCharacterId,
                    action,
                    resolutionDate,
                    issues);
                break;
            case RespondToPoliticalMarriageProposalAction action:
                if (!Enum.IsDefined(action.Response))
                {
                    issues.Add(new(
                        "invalid_proposal_response",
                        "Political-marriage response is invalid."));
                }

                if (!TryRequireActiveProposal(action.ProposalId, issues, out MarriageProposalState? responseProposal))
                {
                    break;
                }

                if (responseProposal.RecipientCharacterId != actingCharacterId)
                {
                    issues.Add(new(
                        "recipient_authority_required",
                        "Only the political-marriage proposal recipient may respond."));
                }

                if (validateAcceptanceEligibility
                    && action.Response == MarriageProposalResponse.Accept
                    && !EvaluateProposalAcceptance(responseProposal, resolutionDate).IsEligible)
                {
                    issues.Add(new(
                        "proposal_acceptance_ineligible",
                        "Political-marriage proposal is not currently eligible for acceptance."));
                }

                break;
            case WithdrawPoliticalMarriageProposalAction action:
                if (TryRequireActiveProposal(
                        action.ProposalId,
                        issues,
                        out MarriageProposalState? withdrawalProposal)
                    && withdrawalProposal.ProposerCharacterId != actingCharacterId)
                {
                    issues.Add(new(
                        "proposer_authority_required",
                        "Only the political-marriage proposer may withdraw the proposal."));
                }

                break;
            case CancelPoliticalBetrothalAction action:
                if (TryRequireActiveBetrothal(
                        action.BetrothalId,
                        issues,
                        out PoliticalBetrothalState? cancellationBetrothal)
                    && !Involves(
                        cancellationBetrothal.FirstCharacterId,
                        cancellationBetrothal.SecondCharacterId,
                        actingCharacterId))
                {
                    issues.Add(new(
                        "participant_authority_required",
                        "Only a political-betrothal participant may cancel it."));
                }

                break;
            case FulfillPoliticalBetrothalAction action:
                if (TryRequireActiveBetrothal(
                        action.BetrothalId,
                        issues,
                        out PoliticalBetrothalState? fulfillmentBetrothal)
                    && !Involves(
                        fulfillmentBetrothal.FirstCharacterId,
                        fulfillmentBetrothal.SecondCharacterId,
                        actingCharacterId))
                {
                    issues.Add(new(
                        "participant_authority_required",
                        "Only a political-betrothal participant may fulfill it."));
                }

                break;
            case OfferRomanceRouteAction action:
                ValidateRomanceOffer(
                    actingCharacterId,
                    action,
                    resolutionDate,
                    issues);
                break;
            case RespondToRomanceInvitationAction action:
                if (!Enum.IsDefined(action.Response))
                {
                    issues.Add(new(
                        "invalid_romance_invitation_response",
                        "Romance-invitation response is invalid."));
                }

                if (!TryRequireRomanceInvitation(
                        action.InvitationId,
                        issues,
                        out RomanceInvitationState? responseInvitation))
                {
                    break;
                }

                if (responseInvitation.RecipientCharacterId != actingCharacterId)
                {
                    issues.Add(new(
                        "romance_recipient_authority_required",
                        "Only the romance-invitation recipient may respond."));
                }

                if (validateAcceptanceEligibility
                    && action.Response == RomanceInvitationResponse.Accept
                    && !EvaluateRomanceEligibility(
                        responseInvitation.InitiatorCharacterId,
                        responseInvitation.RecipientCharacterId,
                        responseInvitation.PracticeId,
                        resolutionDate).IsEligible)
                {
                    issues.Add(new(
                        "romance_invitation_acceptance_ineligible",
                        "Romance invitation is not currently eligible for acceptance."));
                }

                break;
            case WithdrawRomanceInvitationAction action:
                if (TryRequireRomanceInvitation(
                        action.InvitationId,
                        issues,
                        out RomanceInvitationState? withdrawalInvitation)
                    && withdrawalInvitation.InitiatorCharacterId != actingCharacterId)
                {
                    issues.Add(new(
                        "romance_initiator_authority_required",
                        "Only the romance-invitation initiator may withdraw it."));
                }

                break;
            case AdvanceRomanceRouteAction action:
                if (!TryRequireActiveRomanceRoute(
                        action.RouteId,
                        issues,
                        out RomanceRouteState? advancingRoute))
                {
                    break;
                }

                if (!Involves(
                    advancingRoute.FirstCharacterId,
                    advancingRoute.SecondCharacterId,
                    actingCharacterId))
                {
                    issues.Add(new(
                        "romance_participant_authority_required",
                        "Only a romance-route participant may advance it."));
                }

                if (action.ExpectedProgressLevel != advancingRoute.ProgressLevel)
                {
                    issues.Add(new(
                        "stale_romance_progress_level",
                        "Expected romance progress does not match current progress."));
                }

                if (!EvaluateRomanceContinuation(
                    advancingRoute,
                    resolutionDate,
                    authoritativeTurnIndex,
                    new EntityId("command:romance-continuation-validation")).IsEligible)
                {
                    issues.Add(new(
                        "romance_continuation_ineligible",
                        "Romance route is not currently eligible to continue."));
                }

                break;
            case EndRomanceRouteAction action:
                if (TryRequireActiveRomanceRoute(
                        action.RouteId,
                        issues,
                        out RomanceRouteState? endingRoute)
                    && !Involves(
                        endingRoute.FirstCharacterId,
                        endingRoute.SecondCharacterId,
                        actingCharacterId))
                {
                    issues.Add(new(
                        "romance_participant_authority_required",
                        "Only a romance-route participant may end it."));
                }

                break;
            case ImposeCoercedUnionAction action:
                ValidateCoercedUnionAction(
                    actingCharacterId,
                    actor,
                    action,
                    resolutionDate,
                    issues);
                break;
            default:
                issues.Add(new(
                    "unsupported_character_marriage_action",
                    "Only registered character-marriage actions are supported."));
                break;
        }
    }

    private void ValidateCoercedUnionAction(
        EntityId actingCharacterId,
        AuthoritativeCharacterProfile? actor,
        ImposeCoercedUnionAction action,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (actor is not null
            && (actor.Condition.IsIncapacitated
                || actor.Condition.CustodyStatus != CharacterCustodyStatus.Free))
        {
            issues.Add(new(
                "coercive_actor_requires_agency",
                "A coerced-union actor must be capable and free."));
        }

        if (!characters.TryGetCharacterProfile(
                action.RecipientCharacterId,
                out AuthoritativeCharacterProfile? recipient))
        {
            issues.Add(new(
                "unknown_coercive_recipient",
                $"Coerced-union recipient '{action.RecipientCharacterId}' does not exist."));
        }
        else if (recipient.Condition.CustodyStatus == CharacterCustodyStatus.Free
            || recipient.Condition.CustodianId != actingCharacterId)
        {
            issues.Add(new(
                "exact_custodian_authority_required",
                "Only the recipient's exact current custodian may impose a coerced union."));
        }

        MarriageEligibilityResult eligibility = EvaluateEligibility(
            new MarriageEligibilityRequest(
                CharacterMarriageContractVersions.Eligibility,
                MarriageEligibilityCategory.CoercivePoliticalAction,
                actingCharacterId,
                action.RecipientCharacterId,
                action.PracticeId,
                action.ProposedForm,
                action.ConcubinagePrincipalCharacterId),
            resolutionDate);
        foreach (MarriageEligibilityIssue issue in eligibility.Issues)
        {
            issues.Add(new(
                $"coercive_marriage_{issue.Reason.ToString().ToLowerInvariant()}",
                $"Coerced union is ineligible: {issue.Reason}."));
        }

        if (proposals.Values.Any(item => item.Status == MarriageProposalStatus.Active
            && SamePair(
                item.ProposerCharacterId,
                item.RecipientCharacterId,
                actingCharacterId,
                action.RecipientCharacterId)))
        {
            issues.Add(new(
                "duplicate_active_proposal",
                "The coerced-union pair already has an active marriage proposal."));
        }

        foreach (EntityId participantId in new[]
                 {
                     actingCharacterId,
                     action.RecipientCharacterId,
                 }.Distinct())
        {
            if (PinnedProposalCount(participantId)
                >= CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
            {
                issues.Add(new(
                    "retained_proposal_capacity_reached",
                    $"Character '{participantId}' already has the maximum retained active marriage proposals and outcomes."));
            }
        }
    }

    private void ValidateRomanceOffer(
        EntityId actingCharacterId,
        OfferRomanceRouteAction action,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        MarriageEligibilityResult eligibility = EvaluateRomanceEligibility(
            actingCharacterId,
            action.RecipientCharacterId,
            action.PracticeId,
            resolutionDate);
        foreach (MarriageEligibilityIssue issue in eligibility.Issues)
        {
            issues.Add(new(
                $"romance_{issue.Reason.ToString().ToLowerInvariant()}",
                $"Romance invitation is ineligible: {issue.Reason}."));
        }

        if (romanceInvitations.Values.Any(item => SamePair(
            item.InitiatorCharacterId,
            item.RecipientCharacterId,
            actingCharacterId,
            action.RecipientCharacterId)))
        {
            issues.Add(new(
                "duplicate_active_romance_invitation",
                "The participant pair already has an active romance invitation."));
        }

        if (romanceInvitations.Values.Count(item =>
                item.RecipientCharacterId == action.RecipientCharacterId)
            >= CharacterMarriageLimits.ActiveRomanceInvitationsPerRecipient)
        {
            issues.Add(new(
                "active_romance_invitation_recipient_limit_reached",
                "Romance-invitation recipient has eight active invitations."));
        }

        foreach (EntityId participantId in new[]
                 {
                     actingCharacterId,
                     action.RecipientCharacterId,
                 }.Distinct())
        {
            if (romanceInvitations.Values.Count(item => Involves(
                    item.InitiatorCharacterId,
                    item.RecipientCharacterId,
                    participantId))
                >= CharacterMarriageLimits.ActiveRomanceInvitationsPerCharacter)
            {
                issues.Add(new(
                    "active_romance_invitation_character_limit_reached",
                    $"Character '{participantId}' has the maximum active romance invitations."));
            }
        }
    }

    private void ValidateProposalAction(
        EntityId actingCharacterId,
        ProposePoliticalMarriageAction action,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (!Enum.IsDefined(action.Kind))
        {
            issues.Add(new("invalid_proposal_kind", "Marriage proposal kind is invalid."));
            return;
        }

        MarriageEligibilityResult eligibility = EvaluateEligibility(
            new MarriageEligibilityRequest(
                CharacterMarriageContractVersions.Eligibility,
                action.Kind == MarriageProposalKind.LegalUnion
                    ? MarriageEligibilityCategory.VoluntaryLegalUnion
                    : MarriageEligibilityCategory.PoliticalBetrothal,
                actingCharacterId,
                action.RecipientCharacterId,
                action.PracticeId,
                action.ProposedForm,
                action.ConcubinagePrincipalCharacterId),
            resolutionDate);
        foreach (MarriageEligibilityIssue issue in eligibility.Issues)
        {
            issues.Add(new(
                $"marriage_{issue.Reason.ToString().ToLowerInvariant()}",
                $"Political-marriage proposal is ineligible: {issue.Reason}."));
        }

        if (proposals.Values.Count(item =>
                item.Status == MarriageProposalStatus.Active
                && item.RecipientCharacterId == action.RecipientCharacterId)
            >= CharacterMarriageLimits.ActiveProposalsPerRecipient)
        {
            issues.Add(new(
                "active_proposal_limit_reached",
                "Political-marriage recipient has eight active proposals."));
        }

        if (proposals.Values.Any(item =>
            item.Status == MarriageProposalStatus.Active
            && SamePair(
                item.ProposerCharacterId,
                item.RecipientCharacterId,
                actingCharacterId,
                action.RecipientCharacterId)))
        {
            issues.Add(new(
                "duplicate_active_proposal",
                "The participant pair already has an active marriage proposal."));
        }

        foreach (EntityId participantId in new[]
                 {
                     actingCharacterId,
                     action.RecipientCharacterId,
                 }.Distinct())
        {
            if (PinnedProposalCount(participantId)
                >= CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter)
            {
                issues.Add(new(
                    "retained_proposal_capacity_reached",
                    $"Character '{participantId}' already has the maximum retained active marriage proposals and outcomes."));
            }
        }
    }

    private int PinnedProposalCount(EntityId characterId) => proposals.Values.Count(proposal =>
        Involves(
            proposal.ProposerCharacterId,
            proposal.RecipientCharacterId,
            characterId)
        && IsPinnedProposal(proposal));

    private bool IsPinnedProposal(MarriageProposalState proposal)
    {
        if (proposal.Status == MarriageProposalStatus.Active)
        {
            return true;
        }

        if (proposal.Status != MarriageProposalStatus.Accepted)
        {
            return false;
        }

        if (unions.Values.Any(item =>
                item.SourceProposalId == proposal.ProposalId
                && item.Status == MarriageUnionStatus.Active)
            || betrothals.Values.Any(item =>
                item.SourceProposalId == proposal.ProposalId
                && item.Status == PoliticalBetrothalStatus.Active))
        {
            return true;
        }

        return betrothals.Values.Any(item =>
            item.SourceProposalId == proposal.ProposalId
            && item.Status == PoliticalBetrothalStatus.Fulfilled
            && item.FulfillmentUnionId is EntityId unionId
            && unions.TryGetValue(unionId, out MarriageUnionState? union)
            && union.Status == MarriageUnionStatus.Active);
    }

    private bool TryRequireActiveProposal(
        EntityId proposalId,
        ICollection<ValidationIssue> issues,
        [NotNullWhen(true)] out MarriageProposalState? proposal)
    {
        if (!proposalId.IsValid || !proposals.TryGetValue(proposalId, out proposal))
        {
            issues.Add(new(
                "unknown_proposal",
                $"Political-marriage proposal '{proposalId}' does not exist."));
            proposal = null;
            return false;
        }

        if (proposal.Status != MarriageProposalStatus.Active)
        {
            issues.Add(new(
                "proposal_not_active",
                $"Political-marriage proposal '{proposalId}' is already terminal."));
            proposal = null;
            return false;
        }

        if (proposal.Basis != MarriageBasis.Political
            || proposal.ConsentKind != MarriageConsentKind.PoliticalArrangement)
        {
            issues.Add(new(
                "proposal_not_political_arrangement",
                $"Marriage proposal '{proposalId}' is outside the political-arrangement workflow."));
            proposal = null;
            return false;
        }

        return true;
    }

    private bool TryRequireActiveBetrothal(
        EntityId betrothalId,
        ICollection<ValidationIssue> issues,
        [NotNullWhen(true)] out PoliticalBetrothalState? betrothal)
    {
        if (!betrothalId.IsValid || !betrothals.TryGetValue(betrothalId, out betrothal))
        {
            issues.Add(new(
                "unknown_betrothal",
                $"Political betrothal '{betrothalId}' does not exist."));
            betrothal = null;
            return false;
        }

        if (betrothal.Status != PoliticalBetrothalStatus.Active)
        {
            issues.Add(new(
                "betrothal_not_active",
                $"Political betrothal '{betrothalId}' is already terminal."));
            betrothal = null;
            return false;
        }

        return true;
    }

    private bool TryRequireRomanceInvitation(
        EntityId invitationId,
        ICollection<ValidationIssue> issues,
        [NotNullWhen(true)] out RomanceInvitationState? invitation)
    {
        if (!invitationId.IsValid
            || !romanceInvitations.TryGetValue(invitationId, out invitation))
        {
            issues.Add(new(
                "unknown_romance_invitation",
                $"Romance invitation '{invitationId}' does not exist."));
            invitation = null;
            return false;
        }

        return true;
    }

    private bool TryRequireActiveRomanceRoute(
        EntityId routeId,
        ICollection<ValidationIssue> issues,
        [NotNullWhen(true)] out RomanceRouteState? route)
    {
        if (!routeId.IsValid || !romanceRoutes.TryGetValue(routeId, out route))
        {
            issues.Add(new(
                "unknown_romance_route",
                $"Romance route '{routeId}' does not exist."));
            route = null;
            return false;
        }

        if (route.Status != RomanceRouteStatus.Active)
        {
            issues.Add(new(
                "romance_route_not_active",
                $"Romance route '{routeId}' is already terminal."));
            route = null;
            return false;
        }

        return true;
    }

    private MarriageEligibilityResult EvaluateRomanceContinuation(
        RomanceRouteState route,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        RomanceRouteState excluded = route with
        {
            Status = RomanceRouteStatus.Ended,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = authoritativeTurnIndex,
            ResolutionCommandId = commandId,
        };
        CharacterMarriageWorldSnapshot temporary = CaptureSnapshot() with
        {
            RomanceRoutes = romanceRoutes.Values
                .Select(item => item.RouteId == route.RouteId ? excluded : Clone(item))
                .ToArray(),
        };
        CampaignCalendar candidateCalendar = new(
            resolutionDate.CompareTo(calendar.Date) > 0 ? resolutionDate : calendar.Date,
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterMarriageWorldState replacement = new(temporary, characters, candidateCalendar);
        return replacement.EvaluateRomanceEligibility(
            route.FirstCharacterId,
            route.SecondCharacterId,
            route.PracticeId,
            resolutionDate);
    }

    private static MarriageProposalState TerminalizeProposal(
        MarriageProposalState proposal,
        MarriageProposalStatus status,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => proposal with
        {
            Status = status,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = authoritativeTurnIndex,
            ResolutionCommandId = commandId,
        };

    private static PoliticalBetrothalState TerminalizeBetrothal(
        PoliticalBetrothalState betrothal,
        PoliticalBetrothalStatus status,
        EntityId? fulfillmentUnionId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => betrothal with
        {
            Status = status,
            FulfillmentUnionId = fulfillmentUnionId,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = authoritativeTurnIndex,
            ResolutionCommandId = commandId,
        };

    private static RomanceRouteState TerminalizeRomanceRoute(
        RomanceRouteState route,
        RomanceRouteStatus status,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => route with
        {
            Status = status,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = authoritativeTurnIndex,
            ResolutionCommandId = commandId,
        };

    private static MarriageUnionState CreateUnion(
        MarriageProposalState acceptedProposal,
        EntityId first,
        EntityId second,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex) => new(
            CharacterMarriageContractVersions.State,
            CharacterMarriageIds.DeriveMarriageUnionId(acceptedProposal.ProposalId),
            first,
            second,
            acceptedProposal.ProposedForm,
            acceptedProposal.ConcubinagePrincipalCharacterId,
            MarriageBasis.Political,
            MarriageConsentKind.PoliticalArrangement,
            acceptedProposal.PracticeId,
            acceptedProposal.ProposalId,
            resolutionDate,
            authoritativeTurnIndex,
            MarriageUnionStatus.Active,
            null,
            null,
            null,
            null);

    private static MarriageUnionState CreateCoercedUnion(
        MarriageProposalState acceptedProposal,
        EntityId first,
        EntityId second,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex) => new(
            CharacterMarriageContractVersions.State,
            CharacterMarriageIds.DeriveMarriageUnionId(acceptedProposal.ProposalId),
            first,
            second,
            acceptedProposal.ProposedForm,
            acceptedProposal.ConcubinagePrincipalCharacterId,
            MarriageBasis.Political,
            MarriageConsentKind.Coerced,
            acceptedProposal.PracticeId,
            acceptedProposal.ProposalId,
            resolutionDate,
            authoritativeTurnIndex,
            MarriageUnionStatus.Active,
            null,
            null,
            null,
            null);

    private static MarriageUnionState EndUnionForDeath(
        MarriageUnionState union,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => union with
        {
            Status = MarriageUnionStatus.Ended,
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = MarriageUnionEndReason.SpouseDied,
        };

    private static CharacterMarriageWorldSnapshot ApplyLifecycleChanges(
        CharacterMarriageWorldSnapshot snapshot,
        CharacterMarriageLifecycleChangeSet changes)
    {
        if (changes is null
            || changes.ContractVersion
                != CharacterMarriageContractVersions.LifecycleChangeSet
            || !Enum.IsDefined(changes.Reason)
            || changes.InvalidatedProposals is null
            || changes.InvalidatedBetrothals is null
            || changes.EndedUnions is null
            || changes.CancelledInvitations is null
            || changes.InvalidatedRomanceRoutes is null)
        {
            throw new SimulationValidationException(
                "Character-marriage lifecycle change set is malformed.");
        }

        List<MarriageProposalState> proposalList = snapshot.Proposals.Select(Clone).ToList();
        List<PoliticalBetrothalState> betrothalList = snapshot.Betrothals.Select(Clone).ToList();
        List<MarriageUnionState> unionList = snapshot.Unions.Select(Clone).ToList();
        List<RomanceInvitationState> invitationList = snapshot.Invitations.Select(Clone).ToList();
        List<RomanceRouteState> routeList = snapshot.RomanceRoutes.Select(Clone).ToList();
        foreach (MarriageProposalState proposal in changes.InvalidatedProposals)
        {
            Replace(proposalList, proposal, item => item.ProposalId, "marriage proposal");
        }

        foreach (PoliticalBetrothalState betrothal in changes.InvalidatedBetrothals)
        {
            Replace(betrothalList, betrothal, item => item.BetrothalId, "political betrothal");
        }

        foreach (MarriageUnionState union in changes.EndedUnions)
        {
            Replace(unionList, union, item => item.UnionId, "marriage union");
        }

        foreach (RomanceInvitationState invitation in changes.CancelledInvitations)
        {
            Remove(
                invitationList,
                invitation.InvitationId,
                item => item.InvitationId,
                "romance invitation");
        }

        foreach (RomanceRouteState route in changes.InvalidatedRomanceRoutes)
        {
            Replace(routeList, route, item => item.RouteId, "romance route");
        }

        return snapshot with
        {
            Proposals = proposalList,
            Betrothals = betrothalList,
            Unions = unionList,
            Invitations = invitationList,
            RomanceRoutes = routeList,
        };
    }

    private static CharacterMarriageWorldSnapshot ApplyOutcomeToSnapshot(
        CharacterMarriageWorldSnapshot snapshot,
        ICharacterMarriageActionOutcome outcome)
    {
        List<MarriageProposalState> proposalList = snapshot.Proposals.Select(Clone).ToList();
        List<PoliticalBetrothalState> betrothalList = snapshot.Betrothals.Select(Clone).ToList();
        List<MarriageUnionState> unionList = snapshot.Unions.Select(Clone).ToList();
        List<RomanceInvitationState> invitationList = (snapshot.Invitations ?? [])
            .Select(Clone)
            .ToList();
        List<RomanceRouteState> routeList = snapshot.RomanceRoutes.Select(Clone).ToList();
        switch (outcome)
        {
            case MarriageProposalCreatedOutcome value:
                AddNew(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                break;
            case MarriageProposalRefusedOutcome value:
                Replace(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                break;
            case MarriageProposalWithdrawnOutcome value:
                Replace(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                break;
            case MarriageProposalCancelledOutcome value:
                Replace(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                break;
            case PoliticalBetrothalAcceptedOutcome value:
                Replace(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                AddNew(betrothalList, value.Betrothal, item => item.BetrothalId, "political betrothal");
                break;
            case DirectPoliticalUnionAcceptedOutcome value:
                Replace(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                AddNew(unionList, value.Union, item => item.UnionId, "marriage union");
                break;
            case PoliticalBetrothalCancelledOutcome value:
                Replace(betrothalList, value.Betrothal, item => item.BetrothalId, "political betrothal");
                break;
            case PoliticalBetrothalFulfilledOutcome value:
                Replace(betrothalList, value.Betrothal, item => item.BetrothalId, "political betrothal");
                AddNew(proposalList, value.FulfillmentProposal, item => item.ProposalId, "marriage proposal");
                AddNew(unionList, value.Union, item => item.UnionId, "marriage union");
                break;
            case RomanceInvitationCreatedOutcome value:
                AddNew(
                    invitationList,
                    value.Invitation,
                    item => item.InvitationId,
                    "romance invitation");
                break;
            case RomanceInvitationRefusedOutcome value:
                Remove(
                    invitationList,
                    value.Invitation.InvitationId,
                    item => item.InvitationId,
                    "romance invitation");
                break;
            case RomanceInvitationWithdrawnOutcome value:
                Remove(
                    invitationList,
                    value.Invitation.InvitationId,
                    item => item.InvitationId,
                    "romance invitation");
                break;
            case RomanceInvitationCancelledOutcome value:
                Remove(
                    invitationList,
                    value.Invitation.InvitationId,
                    item => item.InvitationId,
                    "romance invitation");
                break;
            case RomanceRouteStartedOutcome value:
                Remove(
                    invitationList,
                    value.InvitationId,
                    item => item.InvitationId,
                    "romance invitation");
                AddNew(routeList, value.Route, item => item.RouteId, "romance route");
                break;
            case RomanceRouteAdvancedOutcome value:
                Replace(routeList, value.Route, item => item.RouteId, "romance route");
                break;
            case RomanceRouteCompletedOutcome value:
                Replace(routeList, value.Route, item => item.RouteId, "romance route");
                break;
            case RomanceRouteEndedOutcome value:
                Replace(routeList, value.Route, item => item.RouteId, "romance route");
                break;
            case CoercedPoliticalUnionImposedOutcome value:
                AddNew(proposalList, value.Proposal, item => item.ProposalId, "marriage proposal");
                AddNew(unionList, value.Union, item => item.UnionId, "marriage union");
                if (value.InvalidatedRomanceRoute is not null)
                {
                    Replace(
                        routeList,
                        value.InvalidatedRomanceRoute,
                        item => item.RouteId,
                        "romance route");
                }

                break;
            default:
                throw new SimulationValidationException(
                    $"Unregistered character-marriage outcome '{outcome.GetType().Name}'.");
        }

        return snapshot with
        {
            Proposals = proposalList,
            Betrothals = betrothalList,
            Unions = unionList,
            RomanceRoutes = routeList,
            Invitations = invitationList,
        };
    }

    private static void AddNew<T>(
        ICollection<T> source,
        T value,
        Func<T, EntityId> id,
        string description)
    {
        if (source.Any(item => id(item) == id(value)))
        {
            throw new SimulationValidationException(
                $"Duplicate {description} '{id(value)}'.");
        }

        source.Add(value);
    }

    private static void Replace<T>(
        IList<T> source,
        T value,
        Func<T, EntityId> id,
        string description)
    {
        int index = source.ToList().FindIndex(item => id(item) == id(value));
        if (index < 0)
        {
            throw new SimulationValidationException(
                $"Missing {description} '{id(value)}'.");
        }

        source[index] = value;
    }

    private static void Remove<T>(
        IList<T> source,
        EntityId valueId,
        Func<T, EntityId> id,
        string description)
    {
        int index = source.ToList().FindIndex(item => id(item) == valueId);
        if (index < 0)
        {
            throw new SimulationValidationException(
                $"Missing {description} '{valueId}'.");
        }

        source.RemoveAt(index);
    }

    private void ReplaceFrom(CharacterMarriageWorldState source)
    {
        CharacterMarriageWorldSnapshot snapshot = source.CaptureSnapshot();
        practices.Clear();
        proposals.Clear();
        betrothals.Clear();
        unions.Clear();
        romanceInvitations.Clear();
        romanceRoutes.Clear();
        history.Clear();
        foreach (MarriagePracticeState item in snapshot.Practices)
        {
            practices.Add(item.PracticeId, Clone(item));
        }

        foreach (MarriageProposalState item in snapshot.Proposals)
        {
            proposals.Add(item.ProposalId, Clone(item));
        }

        foreach (PoliticalBetrothalState item in snapshot.Betrothals)
        {
            betrothals.Add(item.BetrothalId, Clone(item));
        }

        foreach (MarriageUnionState item in snapshot.Unions)
        {
            unions.Add(item.UnionId, Clone(item));
        }

        foreach (RomanceRouteState item in snapshot.RomanceRoutes)
        {
            romanceRoutes.Add(item.RouteId, Clone(item));
        }

        foreach (RomanceInvitationState item in snapshot.Invitations ?? [])
        {
            romanceInvitations.Add(item.InvitationId, Clone(item));
        }

        foreach (CharacterMarriageHistoryAggregate item in snapshot.History)
        {
            history.Add(item.CharacterId, Clone(item));
        }

        calendar = source.calendar;
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<ValidationIssue> issues)
    {
        if (issues.Count > 0)
        {
            throw new SimulationValidationException(string.Join(
                "; ",
                issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
    }

    private static CharacterMarriageWorldSnapshot NormalizeRetention(
        CharacterMarriageWorldSnapshot snapshot)
    {
        Dictionary<EntityId, MarriageProposalState> proposalById = snapshot.Proposals
            .ToDictionary(item => item.ProposalId);
        Dictionary<EntityId, PoliticalBetrothalState> betrothalByProposal = snapshot.Betrothals
            .ToDictionary(item => item.SourceProposalId);
        Dictionary<EntityId, MarriageUnionState> unionByProposal = snapshot.Unions
            .ToDictionary(item => item.SourceProposalId);
        Dictionary<EntityId, MarriageUnionState> unionById = snapshot.Unions
            .ToDictionary(item => item.UnionId);
        Dictionary<EntityId, EntityId> fulfillmentPartners = [];
        foreach (PoliticalBetrothalState betrothal in snapshot.Betrothals.Where(
                     item => item.Status == PoliticalBetrothalStatus.Fulfilled))
        {
            if (betrothal.FulfillmentUnionId is not EntityId unionId
                || !unionById.TryGetValue(unionId, out MarriageUnionState? union)
                || !proposalById.ContainsKey(betrothal.SourceProposalId)
                || !proposalById.ContainsKey(union.SourceProposalId)
                || !fulfillmentPartners.TryAdd(
                    betrothal.SourceProposalId,
                    union.SourceProposalId)
                || !fulfillmentPartners.TryAdd(
                    union.SourceProposalId,
                    betrothal.SourceProposalId))
            {
                throw new SimulationValidationException(
                    $"Fulfilled political betrothal '{betrothal.BetrothalId}' has an invalid retention chain.");
            }
        }

        HashSet<EntityId> retainedProposals = SelectRetainedProposalGroups(
            snapshot.Proposals,
            fulfillmentPartners,
            betrothalByProposal,
            unionByProposal);
        HashSet<EntityId> retainedBetrothals = snapshot.Betrothals
            .Where(item => retainedProposals.Contains(item.SourceProposalId))
            .Select(item => item.BetrothalId)
            .ToHashSet();
        HashSet<EntityId> retainedUnions = snapshot.Unions
            .Where(item => retainedProposals.Contains(item.SourceProposalId))
            .Select(item => item.UnionId)
            .ToHashSet();
        HashSet<EntityId> retainedRoutes = SelectRetained(
            snapshot.RomanceRoutes,
            item => item.RouteId,
            item => (item.FirstCharacterId, item.SecondCharacterId),
            item => item.Status == RomanceRouteStatus.Active,
            item => item.ResolutionTurnIndex ?? item.StartTurnIndex,
            item => item.ResolutionDate ?? item.StartDate,
            "romance routes");

        Dictionary<EntityId, CharacterMarriageHistoryAggregate> folded = snapshot.History
            .ToDictionary(item => item.CharacterId, Clone);
        foreach (MarriageProposalState item in snapshot.Proposals.Where(
                     value => !retainedProposals.Contains(value.ProposalId)))
        {
            FoldRecord(
                folded,
                item.ProposerCharacterId,
                item.RecipientCharacterId,
                item.CreatedDate,
                item.ResolutionDate ?? item.CreatedDate,
                MarriageRecordCategory.Proposal);
        }

        foreach (PoliticalBetrothalState item in snapshot.Betrothals.Where(
                     value => !retainedBetrothals.Contains(value.BetrothalId)))
        {
            FoldRecord(
                folded,
                item.FirstCharacterId,
                item.SecondCharacterId,
                item.StartDate,
                item.ResolutionDate ?? item.StartDate,
                MarriageRecordCategory.Betrothal);
        }

        foreach (MarriageUnionState item in snapshot.Unions.Where(
                     value => !retainedUnions.Contains(value.UnionId)))
        {
            FoldRecord(
                folded,
                item.FirstCharacterId,
                item.SecondCharacterId,
                item.StartDate,
                item.EndDate ?? item.StartDate,
                MarriageRecordCategory.Union);
        }

        foreach (RomanceRouteState item in snapshot.RomanceRoutes.Where(
                     value => !retainedRoutes.Contains(value.RouteId)))
        {
            FoldRecord(
                folded,
                item.FirstCharacterId,
                item.SecondCharacterId,
                item.StartDate,
                item.ResolutionDate ?? item.StartDate,
                MarriageRecordCategory.RomanceRoute);
        }

        return snapshot with
        {
            Proposals = snapshot.Proposals
                .Where(item => retainedProposals.Contains(item.ProposalId))
                .ToArray(),
            Betrothals = snapshot.Betrothals
                .Where(item => retainedBetrothals.Contains(item.BetrothalId))
                .ToArray(),
            Unions = snapshot.Unions
                .Where(item => retainedUnions.Contains(item.UnionId))
                .ToArray(),
            RomanceRoutes = snapshot.RomanceRoutes
                .Where(item => retainedRoutes.Contains(item.RouteId))
                .ToArray(),
            History = folded.Values.OrderBy(item => item.CharacterId).ToArray(),
        };
    }

    private static HashSet<EntityId> SelectRetainedProposalGroups(
        IReadOnlyList<MarriageProposalState> proposals,
        IReadOnlyDictionary<EntityId, EntityId> fulfillmentPartners,
        IReadOnlyDictionary<EntityId, PoliticalBetrothalState> betrothals,
        IReadOnlyDictionary<EntityId, MarriageUnionState> unions)
    {
        Dictionary<EntityId, MarriageProposalState> proposalById = proposals
            .ToDictionary(item => item.ProposalId);
        HashSet<EntityId> grouped = [];
        List<ProposalRetentionGroup> groups = [];
        foreach (MarriageProposalState proposal in proposals.OrderBy(item => item.ProposalId))
        {
            if (!grouped.Add(proposal.ProposalId))
            {
                continue;
            }

            List<MarriageProposalState> members = [proposal];
            if (fulfillmentPartners.TryGetValue(
                    proposal.ProposalId,
                    out EntityId partnerId))
            {
                if (!proposalById.TryGetValue(partnerId, out MarriageProposalState? partner)
                    || !grouped.Add(partnerId))
                {
                    throw new SimulationValidationException(
                        $"Marriage proposal '{proposal.ProposalId}' has an invalid fulfillment retention partner.");
                }

                members.Add(partner);
            }

            (long TerminalTurn, CampaignDate TerminalDate) terminal = members
                .Select(item => (
                    TerminalTurn: ProposalRetentionTurn(item, betrothals, unions),
                    TerminalDate: ProposalRetentionDate(item, betrothals, unions)))
                .OrderByDescending(item => item.TerminalTurn)
                .ThenByDescending(item => item.TerminalDate)
                .First();
            Dictionary<EntityId, int> participantCosts = members
                .SelectMany(item => new[]
                {
                    item.ProposerCharacterId,
                    item.RecipientCharacterId,
                }.Distinct())
                .GroupBy(item => item)
                .ToDictionary(group => group.Key, group => group.Count());
            groups.Add(new ProposalRetentionGroup(
                members.Min(item => item.ProposalId),
                members.Select(item => item.ProposalId).Order().ToArray(),
                participantCosts,
                members.Any(item => IsDirectlyPinnedProposal(item, betrothals, unions)),
                terminal.TerminalTurn,
                terminal.TerminalDate));
        }

        HashSet<EntityId> retained = [];
        Dictionary<EntityId, int> counts = [];
        foreach (ProposalRetentionGroup group in groups
                     .Where(item => item.IsPinned)
                     .OrderBy(item => item.GroupId))
        {
            AddGroup(group, required: true);
        }

        foreach (ProposalRetentionGroup group in groups
                     .Where(item => !item.IsPinned)
                     .OrderByDescending(item => item.TerminalTurn)
                     .ThenByDescending(item => item.TerminalDate)
                     .ThenBy(item => item.GroupId))
        {
            AddGroup(group, required: false);
        }

        return retained;

        void AddGroup(ProposalRetentionGroup group, bool required)
        {
            bool capacity = group.ParticipantCosts.All(item =>
                counts.GetValueOrDefault(item.Key) <=
                CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter - item.Value);
            if (!capacity)
            {
                if (required)
                {
                    throw new SimulationValidationException(
                        $"Active marriage-proposal causal groups exceed the retained per-character bound of {CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter}.");
                }

                return;
            }

            retained.UnionWith(group.ProposalIds);
            foreach ((EntityId characterId, int cost) in group.ParticipantCosts)
            {
                counts[characterId] = checked(counts.GetValueOrDefault(characterId) + cost);
            }
        }
    }

    private static bool IsDirectlyPinnedProposal(
        MarriageProposalState proposal,
        IReadOnlyDictionary<EntityId, PoliticalBetrothalState> betrothals,
        IReadOnlyDictionary<EntityId, MarriageUnionState> unions) =>
        proposal.Status == MarriageProposalStatus.Active
        || proposal.Status == MarriageProposalStatus.Accepted
        && (betrothals.TryGetValue(
                proposal.ProposalId,
                out PoliticalBetrothalState? betrothal)
            && betrothal.Status == PoliticalBetrothalStatus.Active
            || unions.TryGetValue(
                proposal.ProposalId,
                out MarriageUnionState? union)
            && union.Status == MarriageUnionStatus.Active);

    private sealed record ProposalRetentionGroup(
        EntityId GroupId,
        IReadOnlyList<EntityId> ProposalIds,
        IReadOnlyDictionary<EntityId, int> ParticipantCosts,
        bool IsPinned,
        long TerminalTurn,
        CampaignDate TerminalDate);

    private static long ProposalRetentionTurn(
        MarriageProposalState proposal,
        IReadOnlyDictionary<EntityId, PoliticalBetrothalState> betrothals,
        IReadOnlyDictionary<EntityId, MarriageUnionState> unions)
    {
        if (proposal.Status == MarriageProposalStatus.Accepted
            && proposal.Kind == MarriageProposalKind.PoliticalBetrothal
            && betrothals.TryGetValue(
                proposal.ProposalId,
                out PoliticalBetrothalState? betrothal))
        {
            return betrothal.ResolutionTurnIndex ?? betrothal.StartTurnIndex;
        }

        if (proposal.Status == MarriageProposalStatus.Accepted
            && proposal.Kind == MarriageProposalKind.LegalUnion
            && unions.TryGetValue(proposal.ProposalId, out MarriageUnionState? union))
        {
            return union.EndTurnIndex ?? union.StartTurnIndex;
        }

        return proposal.ResolutionTurnIndex ?? proposal.CreatedTurnIndex;
    }

    private static CampaignDate ProposalRetentionDate(
        MarriageProposalState proposal,
        IReadOnlyDictionary<EntityId, PoliticalBetrothalState> betrothals,
        IReadOnlyDictionary<EntityId, MarriageUnionState> unions)
    {
        if (proposal.Status == MarriageProposalStatus.Accepted
            && proposal.Kind == MarriageProposalKind.PoliticalBetrothal
            && betrothals.TryGetValue(
                proposal.ProposalId,
                out PoliticalBetrothalState? betrothal))
        {
            return betrothal.ResolutionDate ?? betrothal.StartDate;
        }

        if (proposal.Status == MarriageProposalStatus.Accepted
            && proposal.Kind == MarriageProposalKind.LegalUnion
            && unions.TryGetValue(proposal.ProposalId, out MarriageUnionState? union))
        {
            return union.EndDate ?? union.StartDate;
        }

        return proposal.ResolutionDate ?? proposal.CreatedDate;
    }

    private static HashSet<EntityId> SelectRetained<T>(
        IEnumerable<T> records,
        Func<T, EntityId> id,
        Func<T, (EntityId First, EntityId Second)> participants,
        Func<T, bool> pinned,
        Func<T, long> terminalTurn,
        Func<T, CampaignDate> terminalDate,
        string description)
    {
        T[] values = records.ToArray();
        HashSet<EntityId> retained = [];
        Dictionary<EntityId, int> counts = [];
        foreach (T item in values.Where(pinned).OrderBy(id))
        {
            AddRetained(item, required: true);
        }

        foreach (T item in values
                     .Where(item => !pinned(item))
                     .OrderByDescending(terminalTurn)
                     .ThenByDescending(terminalDate)
                     .ThenBy(id))
        {
            AddRetained(item, required: false);
        }

        return retained;

        void AddRetained(T item, bool required)
        {
            (EntityId first, EntityId second) = participants(item);
            EntityId[] participantIds = new[] { first, second }.Distinct().ToArray();
            bool capacity = participantIds.All(characterId =>
                counts.GetValueOrDefault(characterId)
                    < CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter);
            if (!capacity)
            {
                if (required)
                {
                    throw new SimulationValidationException(
                        $"Active {description} exceed the retained per-character bound of {CharacterMarriageLimits.RetainedRecordsPerCategoryPerCharacter}.");
                }

                return;
            }

            retained.Add(id(item));
            foreach (EntityId characterId in participantIds)
            {
                counts[characterId] = checked(counts.GetValueOrDefault(characterId) + 1);
            }
        }
    }

    private static void FoldRecord(
        IDictionary<EntityId, CharacterMarriageHistoryAggregate> target,
        EntityId first,
        EntityId second,
        CampaignDate earliest,
        CampaignDate latest,
        MarriageRecordCategory category)
    {
        foreach (EntityId characterId in new[] { first, second }.Distinct())
        {
            CharacterMarriageHistoryAggregate aggregate = target.TryGetValue(
                characterId,
                out CharacterMarriageHistoryAggregate? stored)
                ? stored
                : CharacterMarriageHistoryAggregate.Empty(characterId);
            try
            {
                aggregate = category switch
                {
                    MarriageRecordCategory.Proposal => aggregate with
                    {
                        FoldedProposalCount = checked(aggregate.FoldedProposalCount + 1),
                    },
                    MarriageRecordCategory.Betrothal => aggregate with
                    {
                        FoldedBetrothalCount = checked(aggregate.FoldedBetrothalCount + 1),
                    },
                    MarriageRecordCategory.Union => aggregate with
                    {
                        FoldedUnionCount = checked(aggregate.FoldedUnionCount + 1),
                    },
                    MarriageRecordCategory.RomanceRoute => aggregate with
                    {
                        FoldedRomanceRouteCount = checked(
                            aggregate.FoldedRomanceRouteCount + 1),
                    },
                    _ => throw new SimulationValidationException(
                        "Unregistered character-marriage retention category."),
                };
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Character-marriage history for '{characterId}' exceeds Int64 capacity: {exception.Message}");
            }

            target[characterId] = aggregate with
            {
                EarliestDate = aggregate.EarliestDate is null
                    || earliest.CompareTo(aggregate.EarliestDate.Value) < 0
                        ? earliest
                        : aggregate.EarliestDate,
                LatestDate = aggregate.LatestDate is null
                    || latest.CompareTo(aggregate.LatestDate.Value) > 0
                        ? latest
                        : aggregate.LatestDate,
            };
        }
    }

    private enum MarriageRecordCategory
    {
        Proposal,
        Betrothal,
        Union,
        RomanceRoute,
    }

    private static ICharacterMarriageAction Clone(ICharacterMarriageAction value) => value switch
    {
        ProposePoliticalMarriageAction item => item with { },
        RespondToPoliticalMarriageProposalAction item => item with { },
        WithdrawPoliticalMarriageProposalAction item => item with { },
        CancelPoliticalBetrothalAction item => item with { },
        FulfillPoliticalBetrothalAction item => item with { },
        OfferRomanceRouteAction item => item with { },
        RespondToRomanceInvitationAction item => item with { },
        WithdrawRomanceInvitationAction item => item with { },
        AdvanceRomanceRouteAction item => item with { },
        EndRomanceRouteAction item => item with { },
        ImposeCoercedUnionAction item => item with { },
        _ => throw new SimulationValidationException(
            $"Unregistered character-marriage action '{value.GetType().Name}'."),
    };

    private static ICharacterMarriageActionOutcome Clone(
        ICharacterMarriageActionOutcome value) => value switch
        {
            MarriageProposalCreatedOutcome item => new MarriageProposalCreatedOutcome(Clone(item.Proposal)),
            MarriageProposalRefusedOutcome item => new MarriageProposalRefusedOutcome(Clone(item.Proposal)),
            MarriageProposalWithdrawnOutcome item => new MarriageProposalWithdrawnOutcome(Clone(item.Proposal)),
            MarriageProposalCancelledOutcome item => new MarriageProposalCancelledOutcome(Clone(item.Proposal)),
            PoliticalBetrothalAcceptedOutcome item => new PoliticalBetrothalAcceptedOutcome(
                Clone(item.Proposal),
                Clone(item.Betrothal)),
            DirectPoliticalUnionAcceptedOutcome item => new DirectPoliticalUnionAcceptedOutcome(
                Clone(item.Proposal),
                Clone(item.Union)),
            PoliticalBetrothalCancelledOutcome item => new PoliticalBetrothalCancelledOutcome(Clone(item.Betrothal)),
            PoliticalBetrothalFulfilledOutcome item => new PoliticalBetrothalFulfilledOutcome(
                Clone(item.Betrothal),
                Clone(item.FulfillmentProposal),
                Clone(item.Union)),
            RomanceInvitationCreatedOutcome item => new RomanceInvitationCreatedOutcome(
                Clone(item.Invitation)),
            RomanceInvitationRefusedOutcome item => new RomanceInvitationRefusedOutcome(
                Clone(item.Invitation)),
            RomanceInvitationWithdrawnOutcome item => new RomanceInvitationWithdrawnOutcome(
                Clone(item.Invitation)),
            RomanceInvitationCancelledOutcome item => new RomanceInvitationCancelledOutcome(
                Clone(item.Invitation)),
            RomanceRouteStartedOutcome item => new RomanceRouteStartedOutcome(
                item.InvitationId,
                Clone(item.Route)),
            RomanceRouteAdvancedOutcome item => new RomanceRouteAdvancedOutcome(
                Clone(item.Route)),
            RomanceRouteCompletedOutcome item => new RomanceRouteCompletedOutcome(
                Clone(item.Route)),
            RomanceRouteEndedOutcome item => new RomanceRouteEndedOutcome(Clone(item.Route)),
            CoercedPoliticalUnionImposedOutcome item =>
                new CoercedPoliticalUnionImposedOutcome(
                    Clone(item.Proposal),
                    Clone(item.Union),
                    item.InvalidatedRomanceRoute is null
                        ? null
                        : Clone(item.InvalidatedRomanceRoute)),
            _ => throw new SimulationValidationException(
                $"Unregistered character-marriage outcome '{value.GetType().Name}'."),
        };

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
            || snapshot.Invitations is null
            || snapshot.RomanceRoutes is null
            || snapshot.History is null
            || snapshot.Practices.Any(item => item is null)
            || snapshot.Proposals.Any(item => item is null)
            || snapshot.Betrothals.Any(item => item is null)
            || snapshot.Unions.Any(item => item is null)
            || snapshot.Invitations.Any(item => item is null)
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

    private void AddRomanceInvitations(IReadOnlyList<RomanceInvitationState> source)
    {
        foreach (RomanceInvitationState invitation in source)
        {
            RequireVersion(
                invitation.ContractVersion,
                CharacterMarriageContractVersions.RomanceInvitationState,
                "Romance invitation",
                invitation.InvitationId);
            RequireNamespacedId(
                invitation.InvitationId,
                "romance_invitation:",
                "Romance invitation ID");
            RequireNamespacedId(
                invitation.SourceCommandId,
                "command:",
                "Romance invitation source command ID");
            if (!invitation.InitiatorCharacterId.IsValid
                || !invitation.RecipientCharacterId.IsValid
                || invitation.InitiatorCharacterId == invitation.RecipientCharacterId)
            {
                throw new SimulationValidationException(
                    $"Romance invitation '{invitation.InvitationId}' has invalid participants.");
            }

            RequirePractice(
                invitation.PracticeId,
                $"Romance invitation '{invitation.InvitationId}'");
            ValidateStart(
                invitation.CreatedDate,
                invitation.CreatedTurnIndex,
                $"Romance invitation '{invitation.InvitationId}'");
            if (invitation.InvitationId != CharacterMarriageIds.DeriveRomanceInvitationId(
                invitation.CreatedDate,
                invitation.SourceCommandId))
            {
                throw new SimulationValidationException(
                    $"Romance invitation '{invitation.InvitationId}' does not match its creation evidence.");
            }

            MarriagePracticeState practice = practices[invitation.PracticeId];
            AuthoritativeCharacterProfile initiator = RequireBornCharacter(
                invitation.InitiatorCharacterId,
                invitation.CreatedDate,
                $"Romance invitation '{invitation.InvitationId}' initiator");
            AuthoritativeCharacterProfile recipient = RequireBornCharacter(
                invitation.RecipientCharacterId,
                invitation.CreatedDate,
                $"Romance invitation '{invitation.InvitationId}' recipient");
            RequireMinimumAge(
                initiator,
                invitation.CreatedDate,
                practice.MinimumRomanceAge,
                $"Romance invitation '{invitation.InvitationId}' initiator");
            RequireMinimumAge(
                recipient,
                invitation.CreatedDate,
                practice.MinimumRomanceAge,
                $"Romance invitation '{invitation.InvitationId}' recipient");
            ThrowIfProhibitedKinship(
                practice,
                initiator,
                recipient,
                $"Romance invitation '{invitation.InvitationId}'");
            if (!romanceInvitations.TryAdd(invitation.InvitationId, Clone(invitation)))
            {
                throw new SimulationValidationException(
                    $"Duplicate romance invitation '{invitation.InvitationId}'.");
            }
        }
    }

    private void AddRomanceRoutes(IReadOnlyList<RomanceRouteState> source)
    {
        foreach (RomanceRouteState route in source)
        {
            if (route.ContractVersion is not CharacterMarriageContractVersions.State
                and not CharacterMarriageContractVersions.RomanceRouteState)
            {
                throw new SimulationValidationException(
                    $"Romance route '{route.RouteId}' has unsupported contract version {route.ContractVersion}.");
            }
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
            ValidateRomanceRouteVersionFields(route);
            if (!romanceRoutes.TryAdd(route.RouteId, Clone(route)))
            {
                throw new SimulationValidationException($"Duplicate romance route '{route.RouteId}'.");
            }
        }
    }

    private void ValidateRomanceRouteVersionFields(RomanceRouteState route)
    {
        bool hasAnyV2Field = route.SourceInvitationId is not null
            || route.InvitationInitiatorCharacterId is not null
            || route.InvitationCreatedDate is not null
            || route.InvitationCreatedTurnIndex is not null
            || route.InvitationSourceCommandId is not null
            || route.LastPositiveProgressDate is not null
            || route.LastPositiveProgressTurnIndex is not null
            || route.LastPositiveProgressCommandId is not null;
        if (route.ContractVersion == CharacterMarriageContractVersions.State)
        {
            if (hasAnyV2Field)
            {
                throw new SimulationValidationException(
                    $"Legacy romance route '{route.RouteId}' cannot contain version-2 evidence.");
            }

            return;
        }

        if (route.SourceInvitationId is not EntityId invitationId
            || route.InvitationInitiatorCharacterId is not EntityId initiatorId
            || route.InvitationCreatedDate is not CampaignDate invitationDate
            || route.InvitationCreatedTurnIndex is not long invitationTurn
            || route.InvitationSourceCommandId is not EntityId invitationCommandId
            || route.LastPositiveProgressDate is not CampaignDate progressDate
            || route.LastPositiveProgressTurnIndex is not long progressTurn
            || route.LastPositiveProgressCommandId is not EntityId progressCommandId)
        {
            throw new SimulationValidationException(
                $"Version-2 romance route '{route.RouteId}' is missing historical invitation or progress evidence.");
        }

        RequireNamespacedId(invitationId, "romance_invitation:", "Romance-route source invitation ID");
        RequireNamespacedId(
            invitationCommandId,
            "command:",
            "Romance-route invitation source command ID");
        RequireNamespacedId(
            progressCommandId,
            "command:",
            "Romance-route last-positive-progress command ID");
        if (!Involves(route.FirstCharacterId, route.SecondCharacterId, initiatorId)
            || invitationId != CharacterMarriageIds.DeriveRomanceInvitationId(
                invitationDate,
                invitationCommandId)
            || route.RouteId != CharacterMarriageIds.DeriveRomanceRouteId(
                invitationId,
                route.SourceCommandId)
            || !invitationDate.IsValid
            || invitationTurn < 0
            || invitationDate.CompareTo(route.StartDate) > 0
            || invitationTurn > route.StartTurnIndex
            || !progressDate.IsValid
            || progressTurn < route.StartTurnIndex
            || progressTurn > calendar.TurnIndex
            || progressDate.CompareTo(route.StartDate) < 0
            || progressDate.CompareTo(calendar.Date) > 0
            || route.Status == RomanceRouteStatus.Active
                && route.ProgressLevel is not (>= 1 and <= 3)
            || route.Status == RomanceRouteStatus.Completed
                && route.ProgressLevel != CharacterMarriageLimits.MaximumRomanceProgressLevel
            || route.Status is RomanceRouteStatus.Ended or RomanceRouteStatus.Invalidated
                && route.ProgressLevel is not (>= 1 and <= 3))
        {
            throw new SimulationValidationException(
                $"Version-2 romance route '{route.RouteId}' has inconsistent historical evidence or progress state.");
        }

        if (route.Status == RomanceRouteStatus.Completed
            && (route.ResolutionDate != progressDate
                || route.ResolutionTurnIndex != progressTurn
                || route.ResolutionCommandId != progressCommandId))
        {
            throw new SimulationValidationException(
                $"Completed romance route '{route.RouteId}' must resolve at its last positive progress.");
        }

        if (route.ProgressLevel == 1
            && (progressDate != route.StartDate
                || progressTurn != route.StartTurnIndex
                || progressCommandId != route.SourceCommandId))
        {
            throw new SimulationValidationException(
                $"Level-1 romance route '{route.RouteId}' must identify acceptance as its last positive progress.");
        }

        if (route.ProgressLevel >= 2
            && (progressCommandId == route.SourceCommandId
                || progressCommandId == invitationCommandId))
        {
            throw new SimulationValidationException(
                $"Progressed romance route '{route.RouteId}' must identify a distinct advance command.");
        }

        if (route.ResolutionDate is CampaignDate resolutionDate
            && (progressDate.CompareTo(resolutionDate) > 0
                || progressTurn > route.ResolutionTurnIndex))
        {
            throw new SimulationValidationException(
                $"Romance route '{route.RouteId}' has progress after its terminal resolution.");
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
                calendar.Date,
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
                        || earliest.CompareTo(calendar.Date) > 0))
                || (aggregate.LatestDate is CampaignDate latest
                    && (!latest.IsValid
                        || latest.CompareTo(owner.BirthDate) < 0
                        || latest.CompareTo(calendar.Date) > 0))
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
            if (route.ContractVersion
                == CharacterMarriageContractVersions.RomanceRouteState)
            {
                CampaignDate invitationDate = route.InvitationCreatedDate!.Value;
                AuthoritativeCharacterProfile invitationFirst = RequireBornCharacter(
                    route.FirstCharacterId,
                    invitationDate,
                    $"Romance route '{route.RouteId}' invitation first participant");
                AuthoritativeCharacterProfile invitationSecond = RequireBornCharacter(
                    route.SecondCharacterId,
                    invitationDate,
                    $"Romance route '{route.RouteId}' invitation second participant");
                RequireMinimumAge(
                    invitationFirst,
                    invitationDate,
                    practice.MinimumRomanceAge,
                    $"Romance route '{route.RouteId}' invitation first participant");
                RequireMinimumAge(
                    invitationSecond,
                    invitationDate,
                    practice.MinimumRomanceAge,
                    $"Romance route '{route.RouteId}' invitation second participant");
            }

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

        IEnumerable<(EntityId CommandId, string Label)> creationRecords = proposals.Values
            .Select(item => (
                item.SourceCommandId,
                $"marriage proposal '{item.ProposalId}'"))
            .Concat(romanceInvitations.Values.Select(item => (
                item.SourceCommandId,
                $"romance invitation '{item.InvitationId}'")))
            .Concat(romanceRoutes.Values.Select(item => (
                item.SourceCommandId,
                $"romance route '{item.RouteId}' acceptance")))
            .Concat(romanceRoutes.Values
                .Where(item => item.ContractVersion
                    == CharacterMarriageContractVersions.RomanceRouteState)
                .Select(item => (
                    item.InvitationSourceCommandId!.Value,
                    $"romance route '{item.RouteId}' invitation")))
            .Concat(romanceRoutes.Values
                .Where(item => item.ContractVersion
                    == CharacterMarriageContractVersions.RomanceRouteState
                    && item.ProgressLevel >= 2)
                .Select(item => (
                    item.LastPositiveProgressCommandId!.Value,
                    $"romance route '{item.RouteId}' last positive progress")));
        HashSet<EntityId> creationCommands = [];
        foreach ((EntityId commandId, string label) in creationRecords)
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
        if (romanceInvitations.Values.Any(item =>
                coerciveCommands.Contains(item.SourceCommandId))
            || romanceRoutes.Values.Any(item =>
                coerciveCommands.Contains(item.SourceCommandId)
                || item.InvitationSourceCommandId is EntityId invitationCommandId
                    && coerciveCommands.Contains(invitationCommandId)
                || item.LastPositiveProgressCommandId is EntityId progressCommandId
                    && coerciveCommands.Contains(progressCommandId)
                || item.Status == RomanceRouteStatus.Completed
                    && item.ResolutionCommandId is EntityId resolutionCommandId
                    && coerciveCommands.Contains(resolutionCommandId)))
        {
            throw new SimulationValidationException(
                "A coercive proposal command cannot create or resolve positive romance-route state.");
        }

        if (romanceRoutes.Values.Any(item =>
            item.SourceInvitationId is EntityId invitationId
            && romanceInvitations.ContainsKey(invitationId)))
        {
            throw new SimulationValidationException(
                "An accepted romance invitation cannot remain active beside its route.");
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

        foreach (IGrouping<EntityId, RomanceInvitationState> recipient in
                 romanceInvitations.Values.GroupBy(item => item.RecipientCharacterId))
        {
            if (recipient.Count()
                > CharacterMarriageLimits.ActiveRomanceInvitationsPerRecipient)
            {
                throw new SimulationValidationException(
                    $"Character '{recipient.Key}' exceeds the active romance-invitation recipient limit of {CharacterMarriageLimits.ActiveRomanceInvitationsPerRecipient}.");
            }
        }

        foreach (IGrouping<EntityId, RomanceInvitationState> participant in
                 romanceInvitations.Values
                     .SelectMany(item => new[]
                     {
                         (CharacterId: item.InitiatorCharacterId, Invitation: item),
                         (CharacterId: item.RecipientCharacterId, Invitation: item),
                     })
                     .GroupBy(item => item.CharacterId, item => item.Invitation))
        {
            if (participant.Count()
                > CharacterMarriageLimits.ActiveRomanceInvitationsPerCharacter)
            {
                throw new SimulationValidationException(
                    $"Character '{participant.Key}' exceeds the active romance-invitation involving limit of {CharacterMarriageLimits.ActiveRomanceInvitationsPerCharacter}.");
            }
        }

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
        EnsureUniqueActivePairs(
            romanceInvitations.Values,
            item => CanonicalPair(
                item.InitiatorCharacterId,
                item.RecipientCharacterId),
            "romance invitation");

        HashSet<(EntityId First, EntityId Second)> activeRomancePairs = romanceRoutes.Values
            .Where(item => item.Status == RomanceRouteStatus.Active)
            .Select(item => (item.FirstCharacterId, item.SecondCharacterId))
            .ToHashSet();
        if (romanceInvitations.Values.Any(item => activeRomancePairs.Contains(CanonicalPair(
            item.InitiatorCharacterId,
            item.RecipientCharacterId))))
        {
            throw new SimulationValidationException(
                "A character pair cannot have both an active romance invitation and route.");
        }

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
            || date.CompareTo(calendar.Date) > 0
            || turnIndex < 0
            || turnIndex > calendar.TurnIndex)
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
            || terminalDate.CompareTo(calendar.Date) > 0
            || resolutionTurnIndex is not long terminalTurn
            || terminalTurn < startTurnIndex
            || terminalTurn > calendar.TurnIndex
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
            || endDate.CompareTo(calendar.Date) > 0
            || union.EndTurnIndex is not long endTurn
            || endTurn < union.StartTurnIndex
            || endTurn > calendar.TurnIndex
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
            if (WouldExceedPrincipalLimit(first, practice)
                || WouldExceedPrincipalLimit(second, practice))
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
        int proposedPrincipalCount = checked(principalCount + 1);
        int proposedPartnerCount = checked(partnerCount + 1);
        bool exceedsPrincipalLimit = proposedPrincipalCount
                > practice.MaximumActiveConcubinageUnionsPerPrincipal
            || unions.Values.Any(item =>
                item.Status == MarriageUnionStatus.Active
                && item.Form == MarriageUnionForm.Concubinage
                && item.ConcubinagePrincipalCharacterId == principal
                && proposedPrincipalCount
                    > practices[item.PracticeId].MaximumActiveConcubinageUnionsPerPrincipal);
        bool exceedsPartnerLimit = proposedPartnerCount
                > practice.MaximumActiveConcubinageUnionsPerPartner
            || unions.Values.Any(item =>
                item.Status == MarriageUnionStatus.Active
                && item.Form == MarriageUnionForm.Concubinage
                && item.ConcubinagePrincipalCharacterId != partner
                && Involves(item.FirstCharacterId, item.SecondCharacterId, partner)
                && proposedPartnerCount
                    > practices[item.PracticeId].MaximumActiveConcubinageUnionsPerPartner);
        if (exceedsPrincipalLimit || exceedsPartnerLimit)
        {
            AddIssue(issues, MarriageEligibilityReason.ActiveUnionLimitReached);
        }
    }

    private bool WouldExceedPrincipalLimit(
        EntityId characterId,
        MarriagePracticeState proposedPractice)
    {
        int proposedCount = checked(ActivePrincipalCount(characterId) + 1);
        return proposedCount > proposedPractice.MaximumActivePrincipalSpousesPerCharacter
            || unions.Values.Any(item =>
                item.Status == MarriageUnionStatus.Active
                && item.Form == MarriageUnionForm.PrincipalSpouse
                && Involves(item.FirstCharacterId, item.SecondCharacterId, characterId)
                && proposedCount
                    > practices[item.PracticeId].MaximumActivePrincipalSpousesPerCharacter);
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

    private static RomanceInvitationState Clone(RomanceInvitationState value) => value with { };

    private static RomanceRouteState Clone(RomanceRouteState value) => value with { };

    private static RelationshipMemoryConsequenceSpecification Clone(
        RelationshipMemoryConsequenceSpecification value) => value with
        {
            Impact = value.Impact with { },
            WitnessIds = value.WitnessIds.ToArray(),
        };

    private static CharacterMarriageHistoryAggregate Clone(
        CharacterMarriageHistoryAggregate value) => value with { };

    private static CharacterMarriageLifecycleChangeSet Clone(
        CharacterMarriageLifecycleChangeSet value) => value with
        {
            InvalidatedProposals = value.InvalidatedProposals.Select(Clone).ToArray(),
            InvalidatedBetrothals = value.InvalidatedBetrothals.Select(Clone).ToArray(),
            EndedUnions = value.EndedUnions.Select(Clone).ToArray(),
            CancelledInvitations = value.CancelledInvitations.Select(Clone).ToArray(),
            InvalidatedRomanceRoutes = value.InvalidatedRomanceRoutes.Select(Clone).ToArray(),
        };
}

internal sealed record CharacterMarriageWorldUpdatePlan(
    CharacterMarriageWorldState Candidate);

internal sealed record CharacterMarriageLifecycleUpdatePlan(
    CharacterMarriageLifecycleChangeSet Changes,
    CharacterMarriageWorldUpdatePlan MarriagePlan);
