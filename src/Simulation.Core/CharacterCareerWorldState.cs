using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Simulation.Core;

public sealed class CharacterCareerWorldState : IAuthoritativeCareerWorldQuery
{
    private readonly IAuthoritativeCharacterWorldQuery characters;
    private readonly SortedDictionary<EntityId, CareerProposalState> proposals = [];
    private readonly SortedDictionary<EntityId, RetinueState> retinues = [];
    private readonly SortedDictionary<EntityId, RetinueMembershipState> retinueMemberships = [];
    private readonly SortedDictionary<EntityId, PatronageBondState> patronageBonds = [];
    private readonly SortedDictionary<EntityId, RecommendationRecord> recommendations = [];
    private readonly SortedDictionary<EntityId, EmploymentTenure> employmentTenures = [];
    private readonly SortedDictionary<EntityId, CareerHistoryAggregate> history = [];
    private CampaignCalendar calendar;

    public CharacterCareerWorldState(
        CareerWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar)
    {
        if (snapshot is null)
        {
            throw new SimulationValidationException("Career-world snapshot cannot be null.");
        }

        this.characters = characters
            ?? throw new SimulationValidationException("Authoritative character query cannot be null.");
        if (!calendar.Date.IsValid || calendar.TurnIndex < 0)
        {
            throw new SimulationValidationException("Career-world campaign calendar is invalid.");
        }

        this.calendar = calendar;
        ValidateSnapshotShape(snapshot);
        AddProposals(snapshot.Proposals);
        AddRetinues(snapshot.Retinues);
        AddRetinueMemberships(snapshot.RetinueMemberships);
        AddPatronageBonds(snapshot.PatronageBonds);
        AddRecommendations(snapshot.Recommendations);
        AddEmploymentTenures(snapshot.EmploymentTenures);
        AddHistory(snapshot.History);
        ValidateCrossRecordState();
        ValidateRetentionBounds();
    }

    public IReadOnlyList<CareerProposalState> Proposals =>
        proposals.Values.Select(Clone).ToArray();

    public IReadOnlyList<RetinueState> Retinues =>
        retinues.Values.Select(Clone).ToArray();

    public IReadOnlyList<RetinueMembershipState> RetinueMemberships =>
        retinueMemberships.Values.Select(Clone).ToArray();

    public IReadOnlyList<PatronageBondState> PatronageBonds =>
        patronageBonds.Values.Select(Clone).ToArray();

    public IReadOnlyList<RecommendationRecord> Recommendations =>
        recommendations.Values.Select(Clone).ToArray();

    public IReadOnlyList<EmploymentTenure> EmploymentTenures =>
        employmentTenures.Values.Select(Clone).ToArray();

    public IReadOnlyList<CareerHistoryAggregate> History =>
        history.Values.Select(Clone).ToArray();

    public bool TryGetProposal(
        EntityId proposalId,
        [NotNullWhen(true)] out CareerProposalState? proposal)
    {
        if (proposals.TryGetValue(proposalId, out CareerProposalState? stored))
        {
            proposal = Clone(stored);
            return true;
        }

        proposal = null;
        return false;
    }

    public bool TryGetRetinue(EntityId retinueId, [NotNullWhen(true)] out RetinueState? retinue)
    {
        if (retinues.TryGetValue(retinueId, out RetinueState? stored))
        {
            retinue = Clone(stored);
            return true;
        }

        retinue = null;
        return false;
    }

    public bool TryGetRetinueMembership(
        EntityId membershipId,
        [NotNullWhen(true)] out RetinueMembershipState? membership)
    {
        if (retinueMemberships.TryGetValue(membershipId, out RetinueMembershipState? stored))
        {
            membership = Clone(stored);
            return true;
        }

        membership = null;
        return false;
    }

    public bool TryGetPatronageBond(
        EntityId bondId,
        [NotNullWhen(true)] out PatronageBondState? bond)
    {
        if (patronageBonds.TryGetValue(bondId, out PatronageBondState? stored))
        {
            bond = Clone(stored);
            return true;
        }

        bond = null;
        return false;
    }

    public bool TryGetEmploymentTenure(
        EntityId tenureId,
        [NotNullWhen(true)] out EmploymentTenure? tenure)
    {
        if (employmentTenures.TryGetValue(tenureId, out EmploymentTenure? stored))
        {
            tenure = Clone(stored);
            return true;
        }

        tenure = null;
        return false;
    }

    public bool TryGetHistory(
        EntityId characterId,
        [NotNullWhen(true)] out CareerHistoryAggregate? aggregate)
    {
        if (history.TryGetValue(characterId, out CareerHistoryAggregate? stored))
        {
            aggregate = Clone(stored);
            return true;
        }

        aggregate = null;
        return false;
    }

    public IReadOnlyList<RecommendationRecord> GetRecommendationsInvolving(EntityId characterId) =>
        recommendations.Values
            .Where(item => GetInvolvedCharacters(item).Contains(characterId))
            .Select(Clone)
            .ToArray();

    public CareerWorldSnapshot CaptureSnapshot() => new(
        CareerContractVersions.Snapshot,
        proposals.Values.Select(Clone).ToArray(),
        retinues.Values.Select(Clone).ToArray(),
        retinueMemberships.Values.Select(Clone).ToArray(),
        patronageBonds.Values.Select(Clone).ToArray(),
        recommendations.Values.Select(Clone).ToArray(),
        employmentTenures.Values.Select(Clone).ToArray(),
        history.Values.Select(Clone).ToArray());

    internal void UpdateCampaignCalendar(CampaignCalendar value)
    {
        if (!value.Date.IsValid || value.TurnIndex < 0)
        {
            throw new SimulationValidationException("Career-world campaign calendar is invalid.");
        }

        if (value.Date.CompareTo(calendar.Date) < 0 || value.TurnIndex < calendar.TurnIndex)
        {
            throw new SimulationValidationException(
                "Career-world campaign calendar cannot move backward.");
        }

        calendar = value;
    }

    public CommandValidationResult ValidateAction(
        EntityId actingCharacterId,
        CharacterActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        List<ValidationIssue> issues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            issues,
            requireActorAgency: true);
        if (payload?.Action is null)
        {
            return new(false, issues);
        }

        ValidateCommandRelationshipConsequences(
            payload.RelationshipMemoryConsequences,
            resolutionDate,
            issues);

        switch (payload.Action)
        {
            case RetinueInviteAction action:
                ValidateNewProposal(
                    actingCharacterId,
                    action.RecipientCharacterId,
                    CareerProposalKind.RetinueInvitation,
                    new ServicePrincipalReference(ServicePrincipalKind.Character, actingCharacterId),
                    null,
                    resolutionDate,
                    issues);
                break;
            case RespondToRetinueInvitationAction action:
                ValidateProposalResponse(
                    actingCharacterId,
                    action.ProposalId,
                    CareerProposalKind.RetinueInvitation,
                    action.Response,
                    resolutionDate,
                    issues,
                    forResolution: false);
                break;
            case LeaveRetinueAction action:
                ValidateRetinueLeave(actingCharacterId, action.MembershipId, issues);
                break;
            case PatronageOfferAction action:
                ValidateNewProposal(
                    actingCharacterId,
                    action.RecipientCharacterId,
                    CareerProposalKind.PatronageOffer,
                    new ServicePrincipalReference(ServicePrincipalKind.Character, actingCharacterId),
                    null,
                    resolutionDate,
                    issues);
                break;
            case RespondToPatronageOfferAction action:
                ValidateProposalResponse(
                    actingCharacterId,
                    action.ProposalId,
                    CareerProposalKind.PatronageOffer,
                    action.Response,
                    resolutionDate,
                    issues,
                    forResolution: false);
                break;
            case EndPatronageAction action:
                ValidatePatronageEnd(actingCharacterId, action.BondId, issues);
                break;
            case MakeRecommendationAction action:
                ValidateRecommendation(actingCharacterId, action, resolutionDate, issues);
                break;
            case EmploymentOfferAction action:
                ValidateEmploymentOffer(actingCharacterId, action, resolutionDate, issues);
                break;
            case RespondToEmploymentOfferAction action:
                ValidateProposalResponse(
                    actingCharacterId,
                    action.ProposalId,
                    CareerProposalKind.EmploymentOffer,
                    action.Response,
                    resolutionDate,
                    issues,
                    forResolution: false);
                break;
            case EndEmploymentAction action:
                ValidateEmploymentEnd(actingCharacterId, action.TenureId, issues);
                break;
            case WithdrawCareerProposalAction action:
                ValidateProposalWithdrawal(actingCharacterId, action.ProposalId, issues);
                break;
            default:
                issues.Add(new("unsupported_character_action", "Character action type is not registered."));
                break;
        }

        return issues.Count == 0 ? CommandValidationResult.Valid : new(false, issues);
    }

    public CharacterActionResolvedEventPayload PlanAction(
        EntityId actingCharacterId,
        CharacterActionCommandPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId,
        IReadOnlyList<RelationshipMemoryConsequenceSpecification> relationshipMemoryConsequences)
    {
        if (payload is null)
        {
            throw new SimulationValidationException("Character action payload cannot be null.");
        }

        if (!ConsequenceCollectionsEqual(
            payload.RelationshipMemoryConsequences,
            relationshipMemoryConsequences))
        {
            throw new SimulationValidationException(
                "Character action command and planned relationship consequences must match exactly.");
        }

        ValidateId(commandId, "Character action command ID");
        ValidateId(eventId, "Character action event ID");
        if (eventId != CareerIds.DeriveCharacterActionEventId(resolutionDate, commandId))
        {
            throw new SimulationValidationException(
                $"Character action event ID '{eventId}' does not match command '{commandId}'.");
        }

        ValidateRelationshipConsequences(
            relationshipMemoryConsequences,
            eventId,
            resolutionDate);

        ICharacterAction action = payload?.Action
            ?? throw new SimulationValidationException("Character action payload and action cannot be null.");
        ICharacterActionOutcome outcome;
        if (action is RespondToRetinueInvitationAction retinueResponse)
        {
            outcome = PlanProposalResponse(
                actingCharacterId,
                retinueResponse.ProposalId,
                CareerProposalKind.RetinueInvitation,
                retinueResponse.Response,
                resolutionDate,
                authoritativeTurnIndex,
                commandId);
        }
        else if (action is RespondToPatronageOfferAction patronageResponse)
        {
            outcome = PlanProposalResponse(
                actingCharacterId,
                patronageResponse.ProposalId,
                CareerProposalKind.PatronageOffer,
                patronageResponse.Response,
                resolutionDate,
                authoritativeTurnIndex,
                commandId);
        }
        else if (action is RespondToEmploymentOfferAction employmentResponse)
        {
            outcome = PlanProposalResponse(
                actingCharacterId,
                employmentResponse.ProposalId,
                CareerProposalKind.EmploymentOffer,
                employmentResponse.Response,
                resolutionDate,
                authoritativeTurnIndex,
                commandId);
        }
        else
        {
            CommandValidationResult validation = ValidateAction(
                actingCharacterId,
                payload,
                resolutionDate,
                authoritativeTurnIndex);
            ThrowIfInvalid(validation);
            outcome = PlanNonResponseAction(
                actingCharacterId,
                action,
                resolutionDate,
                authoritativeTurnIndex,
                commandId);
        }

        IReadOnlyList<RelationshipMemoryConsequenceSpecification> resolvedConsequences =
            outcome is CareerProposalInvalidatedOutcome
                ? []
                : relationshipMemoryConsequences;
        return new CharacterActionResolvedEventPayload(
            actingCharacterId,
            Clone(action),
            Clone(outcome),
            resolvedConsequences.Select(Clone).ToArray());
    }

    public void PrevalidateOutcome(
        CharacterActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        _ = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
    }

    internal CharacterCareerWorldUpdatePlan PrepareOutcome(
        CharacterActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (payload is null)
        {
            throw new SimulationValidationException("Character action outcome payload cannot be null.");
        }

        CharacterActionResolvedEventPayload expected = PlanAction(
            payload.ActingCharacterId,
            new CharacterActionCommandPayload(
                payload.Action,
                payload.RelationshipMemoryConsequences),
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId,
            payload.RelationshipMemoryConsequences);
        string expectedJson = JsonSerializer.Serialize(expected, SimulationJson.CreateOptions());
        string actualJson = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(expectedJson, actualJson))
        {
            throw new SimulationValidationException(
                "Character action outcome does not match the exact deterministic plan.");
        }

        return new CharacterCareerWorldUpdatePlan(
            CreateValidatedCandidate(payload.Outcome, resolutionDate, authoritativeTurnIndex));
    }

    internal void ApplyOutcome(
        CharacterActionResolvedEventPayload payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        CharacterCareerWorldUpdatePlan plan = PrepareOutcome(
            payload,
            resolutionDate,
            authoritativeTurnIndex,
            commandId,
            eventId);
        ApplyPrepared(plan);
    }

    internal void ApplyPrepared(CharacterCareerWorldUpdatePlan plan)
    {
        if (plan?.Candidate is null)
        {
            throw new SimulationValidationException("Prepared career update cannot be null.");
        }

        ReplaceFrom(plan.Candidate);
    }

    internal CharacterCareerDeathPlan PrepareCharacterDeath(
        EntityId characterId,
        IAuthoritativeCharacterWorldQuery candidateCharacters,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId,
        EntityId eventId)
    {
        if (!characterId.IsValid
            || candidateCharacters is null
            || !resolutionDate.IsValid
            || resolutionDate.CompareTo(calendar.Date) < 0
            || authoritativeTurnIndex < calendar.TurnIndex
            || !commandId.IsValid
            || eventId != CharacterConditionIds.DeriveActionEventId(
                resolutionDate,
                commandId)
            || !characters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? currentProfile)
            || currentProfile.Condition.VitalStatus != CharacterVitalStatus.Alive
            || !candidateCharacters.TryGetCharacterProfile(
                characterId,
                out AuthoritativeCharacterProfile? candidateProfile)
            || candidateProfile.Condition.VitalStatus != CharacterVitalStatus.Dead)
        {
            throw new SimulationValidationException(
                "Career death preparation requires a valid dead character candidate and current resolution coordinates.");
        }

        CareerWorldSnapshot current = CaptureSnapshot();
        CareerProposalState[] invalidatedProposals = current.Proposals
            .Where(item => item.Status == CareerProposalStatus.Active
                && GetInvolvedCharacters(item).Contains(characterId))
            .Select(item => CompleteProposal(
                item,
                CareerProposalStatus.Invalidated,
                resolutionDate,
                authoritativeTurnIndex,
                commandId))
            .OrderBy(item => item.ProposalId)
            .ToArray();
        RetinueMembershipState[] endedMemberships = current.RetinueMemberships
            .Where(item => item.IsActive
                && GetInvolvedCharacters(item).Contains(characterId))
            .Select(item => EndRetinueMembershipForDeath(
                item,
                characterId,
                resolutionDate,
                authoritativeTurnIndex,
                commandId))
            .OrderBy(item => item.MembershipId)
            .ToArray();
        PatronageBondState[] endedBonds = current.PatronageBonds
            .Where(item => item.IsActive
                && GetInvolvedCharacters(item).Contains(characterId))
            .Select(item => EndPatronageBondForDeath(
                item,
                characterId,
                resolutionDate,
                authoritativeTurnIndex,
                commandId))
            .OrderBy(item => item.BondId)
            .ToArray();
        EmploymentTenure[] endedTenures = current.EmploymentTenures
            .Where(item => item.IsActive
                && GetInvolvedCharacters(item).Contains(characterId))
            .Select(item => EndEmploymentTenureForDeath(
                item,
                characterId,
                resolutionDate,
                authoritativeTurnIndex,
                commandId))
            .OrderBy(item => item.TenureId)
            .ToArray();

        Dictionary<EntityId, CareerProposalState> invalidatedById =
            invalidatedProposals.ToDictionary(item => item.ProposalId);
        Dictionary<EntityId, RetinueMembershipState> endedMembershipsById =
            endedMemberships.ToDictionary(item => item.MembershipId);
        Dictionary<EntityId, PatronageBondState> endedBondsById =
            endedBonds.ToDictionary(item => item.BondId);
        Dictionary<EntityId, EmploymentTenure> endedTenuresById =
            endedTenures.ToDictionary(item => item.TenureId);
        CareerWorldSnapshot candidate = new(
            CareerContractVersions.Snapshot,
            current.Proposals
                .Select(item => invalidatedById.GetValueOrDefault(item.ProposalId) ?? Clone(item))
                .ToArray(),
            current.Retinues.Select(Clone).ToArray(),
            current.RetinueMemberships
                .Select(item => endedMembershipsById.GetValueOrDefault(item.MembershipId) ?? Clone(item))
                .ToArray(),
            current.PatronageBonds
                .Select(item => endedBondsById.GetValueOrDefault(item.BondId) ?? Clone(item))
                .ToArray(),
            current.Recommendations.Select(Clone).ToArray(),
            current.EmploymentTenures
                .Select(item => endedTenuresById.GetValueOrDefault(item.TenureId) ?? Clone(item))
                .ToArray(),
            current.History.Select(Clone).ToArray());
        try
        {
            candidate = NormalizeRetention(candidate);
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Career history counters exceeded their supported range: {exception.Message}");
        }

        CampaignCalendar candidateCalendar = new(
            Max(calendar.Date, resolutionDate),
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        CharacterCareerDeathChangeSet changes = new(
            CareerContractVersions.DeathChange,
            Array.AsReadOnly(invalidatedProposals.Select(Clone).ToArray()),
            Array.AsReadOnly(endedMemberships.Select(Clone).ToArray()),
            Array.AsReadOnly(endedBonds.Select(Clone).ToArray()),
            Array.AsReadOnly(endedTenures.Select(Clone).ToArray()));
        return new CharacterCareerDeathPlan(
            changes,
            new CharacterCareerWorldUpdatePlan(
                new CharacterCareerWorldState(candidate, candidateCharacters, candidateCalendar)));
    }

    private ICharacterActionOutcome PlanNonResponseAction(
        EntityId actingCharacterId,
        ICharacterAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => action switch
        {
            RetinueInviteAction invite => new CareerProposalCreatedOutcome(CreateProposal(
                CareerProposalKind.RetinueInvitation,
                actingCharacterId,
                invite.RecipientCharacterId,
                new ServicePrincipalReference(ServicePrincipalKind.Character, actingCharacterId),
                null,
                resolutionDate,
                authoritativeTurnIndex,
                commandId)),
            PatronageOfferAction offer => new CareerProposalCreatedOutcome(CreateProposal(
                CareerProposalKind.PatronageOffer,
                actingCharacterId,
                offer.RecipientCharacterId,
                new ServicePrincipalReference(ServicePrincipalKind.Character, actingCharacterId),
                null,
                resolutionDate,
                authoritativeTurnIndex,
                commandId)),
            EmploymentOfferAction offer => new CareerProposalCreatedOutcome(CreateProposal(
                CareerProposalKind.EmploymentOffer,
                actingCharacterId,
                offer.RecipientCharacterId,
                Clone(offer.Employer),
                offer.RoleId,
                resolutionDate,
                authoritativeTurnIndex,
                commandId)),
            LeaveRetinueAction leave => new RetinueMembershipEndedOutcome(
                EndRetinueMembership(
                    retinueMemberships[leave.MembershipId],
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId)),
            EndPatronageAction end => new PatronageBondEndedOutcome(
                EndPatronageBond(
                    patronageBonds[end.BondId],
                    actingCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId)),
            MakeRecommendationAction recommendation => new RecommendationRecordedOutcome(
                CreateRecommendation(
                    actingCharacterId,
                    recommendation,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId)),
            EndEmploymentAction end => new EmploymentTenureEndedOutcome(
                EndEmploymentTenure(
                    employmentTenures[end.TenureId],
                    actingCharacterId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId)),
            WithdrawCareerProposalAction withdrawal => new CareerProposalWithdrawnOutcome(
                CompleteProposal(
                    proposals[withdrawal.ProposalId],
                    CareerProposalStatus.Withdrawn,
                    resolutionDate,
                    authoritativeTurnIndex,
                    commandId)),
            _ => throw new SimulationValidationException("Unsupported character action type."),
        };

    private ICharacterActionOutcome PlanProposalResponse(
        EntityId actingCharacterId,
        EntityId proposalId,
        CareerProposalKind expectedKind,
        CareerProposalResponse response,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId)
    {
        List<ValidationIssue> structuralIssues = [];
        ValidateActionEnvelope(
            actingCharacterId,
            new CharacterActionCommandPayload(ResponseAction(expectedKind, proposalId, response)),
            resolutionDate,
            authoritativeTurnIndex,
            structuralIssues,
            requireActorAgency: false);
        ValidateProposalResponse(
            actingCharacterId,
            proposalId,
            expectedKind,
            response,
            resolutionDate,
            structuralIssues,
            forResolution: true);
        ThrowIfInvalid(structuralIssues);

        CareerProposalState proposal = proposals[proposalId];
        if (ResponseMustInvalidate(proposal, actingCharacterId, response, resolutionDate))
        {
            return new CareerProposalInvalidatedOutcome(CompleteProposal(
                proposal,
                CareerProposalStatus.Invalidated,
                resolutionDate,
                authoritativeTurnIndex,
                commandId));
        }

        if (response == CareerProposalResponse.Refuse)
        {
            return new CareerProposalRefusedOutcome(CompleteProposal(
                proposal,
                CareerProposalStatus.Refused,
                resolutionDate,
                authoritativeTurnIndex,
                commandId));
        }

        CareerProposalState accepted = CompleteProposal(
            proposal,
            CareerProposalStatus.Accepted,
            resolutionDate,
            authoritativeTurnIndex,
            commandId);
        return proposal.Kind switch
        {
            CareerProposalKind.RetinueInvitation => CreateRetinueAcceptance(
                accepted,
                resolutionDate,
                authoritativeTurnIndex),
            CareerProposalKind.PatronageOffer => new PatronageOfferAcceptedOutcome(
                accepted,
                new PatronageBondState(
                    CareerContractVersions.State,
                    CareerIds.DerivePatronageBondId(proposal.ProposalId),
                    proposal.ProposerCharacterId,
                    proposal.RecipientCharacterId,
                    proposal.ProposalId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    null,
                    null,
                    null,
                    null)),
            CareerProposalKind.EmploymentOffer => new EmploymentOfferAcceptedOutcome(
                accepted,
                new EmploymentTenure(
                    CareerContractVersions.State,
                    CareerIds.DeriveEmploymentTenureId(proposal.ProposalId),
                    proposal.RecipientCharacterId,
                    Clone(proposal.Principal),
                    proposal.ProposedRoleId!.Value,
                    proposal.ProposalId,
                    resolutionDate,
                    authoritativeTurnIndex,
                    null,
                    null,
                    null,
                    null)),
            _ => throw new SimulationValidationException("Unsupported career proposal kind."),
        };
    }

    private RetinueInvitationAcceptedOutcome CreateRetinueAcceptance(
        CareerProposalState proposal,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        EntityId retinueId = CareerIds.DeriveRetinueId(proposal.ProposerCharacterId);
        RetinueState retinue = retinues.TryGetValue(retinueId, out RetinueState? existing)
            ? Clone(existing)
            : new RetinueState(
                CareerContractVersions.State,
                retinueId,
                proposal.ProposerCharacterId);
        RetinueMembershipState membership = new(
            CareerContractVersions.State,
            CareerIds.DeriveRetinueMembershipId(proposal.ProposalId),
            retinueId,
            proposal.ProposerCharacterId,
            proposal.RecipientCharacterId,
            proposal.ProposalId,
            resolutionDate,
            authoritativeTurnIndex,
            null,
            null,
            null,
            null);
        return new RetinueInvitationAcceptedOutcome(proposal, retinue, membership);
    }

    private static ICharacterAction ResponseAction(
        CareerProposalKind kind,
        EntityId proposalId,
        CareerProposalResponse response) => kind switch
        {
            CareerProposalKind.RetinueInvitation =>
                new RespondToRetinueInvitationAction(proposalId, response),
            CareerProposalKind.PatronageOffer =>
                new RespondToPatronageOfferAction(proposalId, response),
            CareerProposalKind.EmploymentOffer =>
                new RespondToEmploymentOfferAction(proposalId, response),
            _ => throw new SimulationValidationException("Unsupported career proposal kind."),
        };

    private CareerProposalState CreateProposal(
        CareerProposalKind kind,
        EntityId proposerCharacterId,
        EntityId recipientCharacterId,
        ServicePrincipalReference principal,
        EntityId? proposedRoleId,
        CampaignDate createdDate,
        long createdTurnIndex,
        EntityId sourceCommandId) => new(
        CareerContractVersions.State,
        CareerIds.DeriveProposalId(kind, createdDate, sourceCommandId),
        kind,
        proposerCharacterId,
        recipientCharacterId,
        Clone(principal),
        proposedRoleId,
        createdDate,
        createdTurnIndex,
        sourceCommandId,
        CareerProposalStatus.Active,
        null,
        null,
        null);

    private static CareerProposalState CompleteProposal(
        CareerProposalState proposal,
        CareerProposalStatus status,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => proposal with
        {
            Status = status,
            ResolutionDate = resolutionDate,
            ResolutionTurnIndex = authoritativeTurnIndex,
            ResolutionCommandId = commandId,
        };

    private static RetinueMembershipState EndRetinueMembership(
        RetinueMembershipState membership,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => membership with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = CareerServiceEndReason.MemberLeft,
        };

    private static RetinueMembershipState EndRetinueMembershipForDeath(
        RetinueMembershipState membership,
        EntityId characterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => membership with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = membership.LeaderCharacterId == characterId
                ? CareerServiceEndReason.LeaderDied
                : CareerServiceEndReason.MemberDied,
        };

    private static PatronageBondState EndPatronageBond(
        PatronageBondState bond,
        EntityId actingCharacterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => bond with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = actingCharacterId == bond.PatronCharacterId
                ? CareerServiceEndReason.PatronEnded
                : CareerServiceEndReason.BeneficiaryEnded,
        };

    private static PatronageBondState EndPatronageBondForDeath(
        PatronageBondState bond,
        EntityId characterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => bond with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = bond.PatronCharacterId == characterId
                ? CareerServiceEndReason.PatronDied
                : CareerServiceEndReason.BeneficiaryDied,
        };

    private static EmploymentTenure EndEmploymentTenure(
        EmploymentTenure tenure,
        EntityId actingCharacterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => tenure with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = actingCharacterId == tenure.EmployeeCharacterId
                ? CareerServiceEndReason.EmployeeLeft
                : CareerServiceEndReason.EmployerEnded,
        };

    private static EmploymentTenure EndEmploymentTenureForDeath(
        EmploymentTenure tenure,
        EntityId characterId,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => tenure with
        {
            EndDate = resolutionDate,
            EndTurnIndex = authoritativeTurnIndex,
            EndCommandId = commandId,
            EndReason = tenure.EmployeeCharacterId == characterId
                ? CareerServiceEndReason.EmployeeDied
                : CareerServiceEndReason.EmployerDied,
        };

    private static RecommendationRecord CreateRecommendation(
        EntityId actingCharacterId,
        MakeRecommendationAction action,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        EntityId commandId) => new(
        CareerContractVersions.State,
        CareerIds.DeriveRecommendationId(resolutionDate, commandId),
        actingCharacterId,
        action.BeneficiaryCharacterId,
        Clone(action.Principal),
        action.RecommendedRoleId,
        resolutionDate,
        authoritativeTurnIndex,
        commandId);

    private void ValidateActionEnvelope(
        EntityId actingCharacterId,
        CharacterActionCommandPayload? payload,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex,
        ICollection<ValidationIssue> issues,
        bool requireActorAgency)
    {
        if (!actingCharacterId.IsValid)
        {
            issues.Add(new("invalid_actor", "Character action actor ID is invalid."));
        }

        if (!resolutionDate.IsValid)
        {
            issues.Add(new("invalid_resolution_date", "Character action resolution date is invalid."));
        }
        else if (resolutionDate.CompareTo(calendar.Date) < 0)
        {
            issues.Add(new("past_resolution_date", "Character action resolution date precedes career state."));
        }

        if (authoritativeTurnIndex < calendar.TurnIndex)
        {
            issues.Add(new("past_turn_index", "Character action turn precedes career state."));
        }

        if (payload is null)
        {
            issues.Add(new("invalid_payload", "Character action payload cannot be null."));
            return;
        }

        if (payload.Action is null)
        {
            issues.Add(new("invalid_action", "Character action cannot be null."));
        }

        if (!actingCharacterId.IsValid
            || !characters.TryGetCharacterProfile(
                actingCharacterId,
                out AuthoritativeCharacterProfile? actor))
        {
            issues.Add(new("unknown_actor", $"Character action actor '{actingCharacterId}' does not exist."));
            return;
        }

        if (resolutionDate.IsValid && actor.BirthDate.CompareTo(resolutionDate) > 0)
        {
            issues.Add(new("actor_not_born", "Character action actor is not born by the resolution date."));
        }

        if (requireActorAgency)
        {
            AddAgencyIssues(actor, "actor", issues);
        }
    }

    private void ValidateNewProposal(
        EntityId proposerCharacterId,
        EntityId recipientCharacterId,
        CareerProposalKind kind,
        ServicePrincipalReference principal,
        EntityId? roleId,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (!Enum.IsDefined(kind))
        {
            issues.Add(new("invalid_proposal_kind", "Career proposal kind is invalid."));
        }

        if (!recipientCharacterId.IsValid)
        {
            issues.Add(new("invalid_recipient", "Career proposal recipient ID is invalid."));
            return;
        }

        if (proposerCharacterId == recipientCharacterId)
        {
            issues.Add(new("self_proposal", "A character cannot make this proposal to themself."));
        }

        if (!characters.TryGetCharacterProfile(
            recipientCharacterId,
            out AuthoritativeCharacterProfile? recipient))
        {
            issues.Add(new("unknown_recipient", $"Career proposal recipient '{recipientCharacterId}' does not exist."));
        }
        else
        {
            if (resolutionDate.IsValid && recipient.BirthDate.CompareTo(resolutionDate) > 0)
            {
                issues.Add(new("recipient_not_born", "Career proposal recipient is not born."));
            }

            AddAgencyIssues(recipient, "recipient", issues);
            if (kind is CareerProposalKind.RetinueInvitation or CareerProposalKind.EmploymentOffer
                && recipient.Condition.CustodyStatus != CharacterCustodyStatus.Free)
            {
                issues.Add(new(
                    "recipient_custody",
                    "Retinue and employment proposals require a free recipient."));
            }
        }

        if (characters.TryGetCharacterProfile(
                proposerCharacterId,
                out AuthoritativeCharacterProfile? proposer)
            && proposer.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            issues.Add(new("actor_custody", "A character in custody cannot initiate this proposal."));
        }

        ValidatePrincipal(principal, issues);
        if (kind == CareerProposalKind.EmploymentOffer)
        {
            if (roleId is not EntityId role || !role.IsValid)
            {
                issues.Add(new("invalid_role", "Employment proposals require a valid role ID."));
            }
        }
        else if (roleId is not null)
        {
            issues.Add(new("unexpected_role", "Only employment proposals may carry a role ID."));
        }

        if (proposals.Values.Count(item =>
                item.Status == CareerProposalStatus.Active
                && item.RecipientCharacterId == recipientCharacterId)
            >= CareerLimits.ActiveProposalsPerRecipient)
        {
            issues.Add(new("proposal_recipient_limit", "Career proposal recipient already has eight active proposals."));
        }

        if (proposals.Values.Any(item =>
            item.Status == CareerProposalStatus.Active
            && item.Kind == kind
            && item.ProposerCharacterId == proposerCharacterId
            && item.RecipientCharacterId == recipientCharacterId
            && item.Principal == principal
            && item.ProposedRoleId == roleId))
        {
            issues.Add(new("duplicate_active_proposal", "An equivalent active career proposal already exists."));
        }
    }

    private void ValidateProposalResponse(
        EntityId actingCharacterId,
        EntityId proposalId,
        CareerProposalKind expectedKind,
        CareerProposalResponse response,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues,
        bool forResolution)
    {
        if (!proposalId.IsValid)
        {
            issues.Add(new("invalid_proposal", "Career proposal ID is invalid."));
            return;
        }

        if (!Enum.IsDefined(response))
        {
            issues.Add(new("invalid_response", "Career proposal response is invalid."));
        }

        if (!proposals.TryGetValue(proposalId, out CareerProposalState? proposal))
        {
            issues.Add(new("unknown_proposal", $"Career proposal '{proposalId}' does not exist."));
            return;
        }

        if (proposal.Kind != expectedKind)
        {
            issues.Add(new("proposal_kind_mismatch", "Response action does not match the proposal kind."));
        }

        if (proposal.Status != CareerProposalStatus.Active)
        {
            issues.Add(new("proposal_not_active", "Career proposal is no longer active."));
        }

        if (proposal.RecipientCharacterId != actingCharacterId)
        {
            issues.Add(new("not_proposal_recipient", "Only the proposal recipient may respond."));
        }

        if (!forResolution
            && Enum.IsDefined(response)
            && ResponseMustInvalidate(proposal, actingCharacterId, response, resolutionDate))
        {
            issues.Add(new(
                "proposal_invalidated",
                "Career proposal can no longer resolve as a voluntary response."));
        }
    }

    private void ValidateRetinueLeave(
        EntityId actingCharacterId,
        EntityId membershipId,
        ICollection<ValidationIssue> issues)
    {
        if (!membershipId.IsValid)
        {
            issues.Add(new("invalid_membership", "Retinue membership ID is invalid."));
            return;
        }

        if (!retinueMemberships.TryGetValue(
            membershipId,
            out RetinueMembershipState? membership))
        {
            issues.Add(new("unknown_membership", $"Retinue membership '{membershipId}' does not exist."));
        }
        else if (!membership.IsActive)
        {
            issues.Add(new("membership_not_active", "Retinue membership has already ended."));
        }
        else if (membership.MemberCharacterId != actingCharacterId)
        {
            issues.Add(new("not_retinue_member", "Only the member may voluntarily leave a retinue."));
        }
    }

    private void ValidatePatronageEnd(
        EntityId actingCharacterId,
        EntityId bondId,
        ICollection<ValidationIssue> issues)
    {
        if (!bondId.IsValid)
        {
            issues.Add(new("invalid_patronage_bond", "Patronage bond ID is invalid."));
            return;
        }

        if (!patronageBonds.TryGetValue(bondId, out PatronageBondState? bond))
        {
            issues.Add(new("unknown_patronage_bond", $"Patronage bond '{bondId}' does not exist."));
        }
        else if (!bond.IsActive)
        {
            issues.Add(new("patronage_not_active", "Patronage bond has already ended."));
        }
        else if (actingCharacterId != bond.PatronCharacterId
            && actingCharacterId != bond.BeneficiaryCharacterId)
        {
            issues.Add(new("not_patronage_party", "Only a party to patronage may end it."));
        }
    }

    private void ValidateRecommendation(
        EntityId actingCharacterId,
        MakeRecommendationAction action,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (characters.TryGetCharacterProfile(
                actingCharacterId,
                out AuthoritativeCharacterProfile? actor)
            && actor.Condition.CustodyStatus != CharacterCustodyStatus.Free)
        {
            issues.Add(new("actor_custody", "A character in custody cannot make a recommendation."));
        }

        if (!action.BeneficiaryCharacterId.IsValid)
        {
            issues.Add(new("invalid_beneficiary", "Recommendation beneficiary ID is invalid."));
        }
        else if (!characters.TryGetCharacterProfile(
            action.BeneficiaryCharacterId,
            out AuthoritativeCharacterProfile? beneficiary))
        {
            issues.Add(new(
                "unknown_beneficiary",
                $"Recommendation beneficiary '{action.BeneficiaryCharacterId}' does not exist."));
        }
        else
        {
            if (beneficiary.BirthDate.CompareTo(resolutionDate) > 0)
            {
                issues.Add(new("beneficiary_not_born", "Recommendation beneficiary is not born."));
            }

            AddAgencyIssues(beneficiary, "beneficiary", issues);
        }

        if (actingCharacterId == action.BeneficiaryCharacterId)
        {
            issues.Add(new("self_recommendation", "A recommendation requires a different beneficiary."));
        }

        ValidatePrincipal(action.Principal, issues);
        if (action.Principal is not null
            && action.Principal.Kind == ServicePrincipalKind.Character
            && action.Principal.PrincipalId == action.BeneficiaryCharacterId)
        {
            issues.Add(new("self_service_principal", "A character cannot be recommended to themself."));
        }

        if (action.RecommendedRoleId is EntityId roleId && !roleId.IsValid)
        {
            issues.Add(new("invalid_role", "Recommended role ID is invalid."));
        }
    }

    private void ValidateEmploymentOffer(
        EntityId actingCharacterId,
        EmploymentOfferAction action,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (action.Employer is null)
        {
            issues.Add(new("invalid_principal", "Employment employer cannot be null."));
            return;
        }

        ValidateNewProposal(
            actingCharacterId,
            action.RecipientCharacterId,
            CareerProposalKind.EmploymentOffer,
            action.Employer,
            action.RoleId,
            resolutionDate,
            issues);
        if (!IsAuthorizedPrincipalActor(actingCharacterId, action.Employer))
        {
            issues.Add(new(
                "unauthorized_employer_actor",
                "Employment offers require the character principal or current household head."));
        }
    }

    private void ValidateEmploymentEnd(
        EntityId actingCharacterId,
        EntityId tenureId,
        ICollection<ValidationIssue> issues)
    {
        if (!tenureId.IsValid)
        {
            issues.Add(new("invalid_tenure", "Employment tenure ID is invalid."));
            return;
        }

        if (!employmentTenures.TryGetValue(tenureId, out EmploymentTenure? tenure))
        {
            issues.Add(new("unknown_tenure", $"Employment tenure '{tenureId}' does not exist."));
        }
        else if (!tenure.IsActive)
        {
            issues.Add(new("tenure_not_active", "Employment tenure has already ended."));
        }
        else if (tenure.EmployeeCharacterId != actingCharacterId
            && !IsAuthorizedPrincipalActor(actingCharacterId, tenure.Employer))
        {
            issues.Add(new(
                "not_employment_party",
                "Only the employee or current employer representative may end employment."));
        }
    }

    private void ValidateProposalWithdrawal(
        EntityId actingCharacterId,
        EntityId proposalId,
        ICollection<ValidationIssue> issues)
    {
        if (!proposalId.IsValid)
        {
            issues.Add(new("invalid_proposal", "Career proposal ID is invalid."));
            return;
        }

        if (!proposals.TryGetValue(proposalId, out CareerProposalState? proposal))
        {
            issues.Add(new("unknown_proposal", $"Career proposal '{proposalId}' does not exist."));
        }
        else if (proposal.Status != CareerProposalStatus.Active)
        {
            issues.Add(new("proposal_not_active", "Career proposal is no longer active."));
        }
        else if (proposal.ProposerCharacterId != actingCharacterId)
        {
            issues.Add(new("not_proposal_proposer", "Only the proposer may withdraw a proposal."));
        }
    }

    private bool ResponseMustInvalidate(
        CareerProposalState proposal,
        EntityId actingCharacterId,
        CareerProposalResponse response,
        CampaignDate resolutionDate)
    {
        if (!characters.TryGetCharacterProfile(
                actingCharacterId,
                out AuthoritativeCharacterProfile? recipient)
            || !CanExerciseAgency(recipient, resolutionDate)
            || proposal.RecipientCharacterId != actingCharacterId
            || !characters.TryGetCharacterProfile(
                proposal.ProposerCharacterId,
                out AuthoritativeCharacterProfile? proposer)
            || !CanExerciseAgency(proposer, resolutionDate)
            || !PrincipalStillValid(proposal))
        {
            return true;
        }

        if (response == CareerProposalResponse.Refuse)
        {
            return false;
        }

        return proposal.Kind switch
        {
            CareerProposalKind.RetinueInvitation =>
                recipient.Condition.CustodyStatus != CharacterCustodyStatus.Free
                || ActiveRetinueMembershipCount(proposal.ProposerCharacterId)
                    >= CareerLimits.ActiveMembershipsPerRetinue
                || HasActiveRetinueMembership(
                    proposal.ProposerCharacterId,
                    proposal.RecipientCharacterId),
            CareerProposalKind.PatronageOffer =>
                ActivePatronageCount(proposal.ProposerCharacterId)
                    >= CareerLimits.ActivePatronageBondsPerCharacter
                || ActivePatronageCount(proposal.RecipientCharacterId)
                    >= CareerLimits.ActivePatronageBondsPerCharacter
                || HasActivePatronageBond(
                    proposal.ProposerCharacterId,
                    proposal.RecipientCharacterId),
            CareerProposalKind.EmploymentOffer =>
                recipient.Condition.CustodyStatus != CharacterCustodyStatus.Free
                || ActiveEmploymentCount(proposal.RecipientCharacterId)
                    >= CareerLimits.ActiveEmploymentTenuresPerEmployee
                || HasActiveEmploymentTenure(
                    proposal.RecipientCharacterId,
                    proposal.Principal,
                    proposal.ProposedRoleId!.Value),
            _ => true,
        };
    }

    private bool PrincipalStillValid(CareerProposalState proposal)
    {
        if (!TryResolvePrincipal(proposal.Principal, out EntityId representativeId))
        {
            return false;
        }

        if (proposal.Kind == CareerProposalKind.EmploymentOffer
            && representativeId != proposal.ProposerCharacterId)
        {
            return false;
        }

        return characters.TryGetCharacterProfile(
                representativeId,
                out AuthoritativeCharacterProfile? representative)
            && CanExerciseAgency(representative, calendar.Date);
    }

    private void ValidatePrincipal(
        ServicePrincipalReference? principal,
        ICollection<ValidationIssue> issues)
    {
        if (principal is null)
        {
            issues.Add(new("invalid_principal", "Service principal cannot be null."));
            return;
        }

        if (!Enum.IsDefined(principal.Kind))
        {
            issues.Add(new("invalid_principal_kind", "Service principal kind is invalid."));
        }

        if (!principal.PrincipalId.IsValid)
        {
            issues.Add(new("invalid_principal", "Service principal ID is invalid."));
            return;
        }

        if (principal.Kind == ServicePrincipalKind.Character)
        {
            if (!characters.TryGetCharacterProfile(principal.PrincipalId, out _))
            {
                issues.Add(new(
                    "unknown_character_principal",
                    $"Character principal '{principal.PrincipalId}' does not exist."));
            }
        }
        else if (principal.Kind == ServicePrincipalKind.Household
            && !characters.TryGetHousehold(principal.PrincipalId, out _))
        {
            issues.Add(new(
                "unknown_household_principal",
                $"Household principal '{principal.PrincipalId}' does not exist."));
        }
    }

    private bool IsAuthorizedPrincipalActor(
        EntityId actingCharacterId,
        ServicePrincipalReference principal) =>
        TryResolvePrincipal(principal, out EntityId representativeId)
        && representativeId == actingCharacterId;

    private bool TryResolvePrincipal(
        ServicePrincipalReference principal,
        out EntityId representativeId)
    {
        if (principal is null || !principal.PrincipalId.IsValid)
        {
            representativeId = default;
            return false;
        }

        if (principal.Kind == ServicePrincipalKind.Character
            && characters.TryGetCharacterProfile(principal.PrincipalId, out _))
        {
            representativeId = principal.PrincipalId;
            return true;
        }

        if (principal.Kind == ServicePrincipalKind.Household
            && characters.TryGetHousehold(
                principal.PrincipalId,
                out AuthoritativeHouseholdView? household))
        {
            representativeId = household.HeadCharacterId;
            return true;
        }

        representativeId = default;
        return false;
    }

    private void ValidateRelationshipConsequences(
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? consequences,
        EntityId eventId,
        CampaignDate resolutionDate)
    {
        if (consequences is null || consequences.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Relationship memory consequence list and entries cannot be null.");
        }

        if (consequences.Count > CareerLimits.RelationshipConsequencesPerAction)
        {
            throw new SimulationValidationException(
                $"Character actions retain at most {CareerLimits.RelationshipConsequencesPerAction} relationship consequences.");
        }

        for (int index = 0; index < consequences.Count; index++)
        {
            RelationshipMemoryConsequenceSpecification consequence = consequences[index];
            if (consequence.ConsequenceId
                != CareerIds.DeriveRelationshipConsequenceId(eventId, index))
            {
                throw new SimulationValidationException(
                    "Relationship memory consequence ID or ordering is not deterministic.");
            }

            ValidateRelationshipConsequenceShape(consequence, resolutionDate);
        }
    }

    private void ValidateCommandRelationshipConsequences(
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? consequences,
        CampaignDate resolutionDate,
        ICollection<ValidationIssue> issues)
    {
        if (consequences is null || consequences.Any(item => item is null))
        {
            issues.Add(new(
                "invalid_relationship_consequences",
                "Relationship memory consequence list and entries cannot be null."));
            return;
        }

        if (consequences.Count > CareerLimits.RelationshipConsequencesPerAction)
        {
            issues.Add(new(
                "relationship_consequence_limit",
                $"Character actions retain at most {CareerLimits.RelationshipConsequencesPerAction} relationship consequences."));
            return;
        }

        HashSet<EntityId> consequenceIds = [];
        foreach (RelationshipMemoryConsequenceSpecification consequence in consequences)
        {
            if (!consequence.ConsequenceId.IsValid || !consequenceIds.Add(consequence.ConsequenceId))
            {
                issues.Add(new(
                    "noncanonical_relationship_consequences",
                    "Relationship memory consequence IDs must be valid and unique."));
                continue;
            }

            try
            {
                ValidateRelationshipConsequenceShape(consequence, resolutionDate);
            }
            catch (SimulationValidationException exception)
            {
                issues.Add(new("invalid_relationship_consequence", exception.Message));
            }
        }
    }

    private void ValidateRelationshipConsequenceShape(
        RelationshipMemoryConsequenceSpecification consequence,
        CampaignDate resolutionDate)
    {
        if (consequence.ContractVersion != CareerContractVersions.RelationshipConsequence)
        {
            throw new SimulationValidationException(
                "Unsupported relationship memory consequence contract version.");
        }

        ValidateId(consequence.ConsequenceId, "Relationship memory consequence ID");
        ValidateCharacterAtDate(
            consequence.SubjectCharacterId,
            resolutionDate,
            "Relationship consequence subject");
        ValidateCharacterAtDate(
            consequence.TargetCharacterId,
            resolutionDate,
            "Relationship consequence target");
        if (consequence.SubjectCharacterId == consequence.TargetCharacterId)
        {
            throw new SimulationValidationException(
                "Relationship memory consequence must be directional between different characters.");
        }

        if (consequence.Impact is null || !ImpactCanRepresentBoundedTransition(consequence.Impact))
        {
            throw new SimulationValidationException(
                "Relationship memory consequence impact is outside supported bounds.");
        }

        ValidateId(consequence.MeaningId, "Relationship memory meaning ID");
        if (consequence.InitialSeverity is < 1 or > 100
            || !Enum.IsDefined(consequence.Publicity)
            || consequence.DecayIntervalTurns < 0)
        {
            throw new SimulationValidationException(
                "Relationship memory consequence severity, publicity, or decay is invalid.");
        }

        ValidateConsequenceWitnesses(consequence, resolutionDate);
    }

    private static bool ConsequenceCollectionsEqual(
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? left,
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return StringComparer.Ordinal.Equals(
            JsonSerializer.Serialize(left, SimulationJson.CreateOptions()),
            JsonSerializer.Serialize(right, SimulationJson.CreateOptions()));
    }

    private void ValidateConsequenceWitnesses(
        RelationshipMemoryConsequenceSpecification consequence,
        CampaignDate resolutionDate)
    {
        if (consequence.WitnessIds is null)
        {
            throw new SimulationValidationException(
                "Relationship memory consequence witness IDs cannot be null.");
        }

        if (consequence.Publicity == MemoryPublicity.Witnessed)
        {
            if (consequence.WitnessIds.Count is < 1 or > CareerLimits.RelationshipWitnesses)
            {
                throw new SimulationValidationException(
                    "Witnessed relationship consequences require from one through 32 witnesses.");
            }
        }
        else if (consequence.WitnessIds.Count != 0)
        {
            throw new SimulationValidationException(
                "Only witnessed relationship consequences may retain witness IDs.");
        }

        EntityId? previous = null;
        foreach (EntityId witnessId in consequence.WitnessIds)
        {
            if (previous is EntityId previousId && previousId.CompareTo(witnessId) >= 0)
            {
                throw new SimulationValidationException(
                    "Relationship consequence witnesses must be unique and canonical.");
            }

            ValidateCharacterAtDate(witnessId, resolutionDate, "Relationship consequence witness");
            if (witnessId == consequence.SubjectCharacterId
                || witnessId == consequence.TargetCharacterId)
            {
                throw new SimulationValidationException(
                    "Relationship consequence witnesses cannot repeat a participant.");
            }

            previous = witnessId;
        }
    }

    private static bool ImpactCanRepresentBoundedTransition(RelationshipImpact impact) =>
        impact.Affection is >= -100 and <= 100
        && impact.Trust is >= -100 and <= 100
        && impact.Respect is >= -100 and <= 100
        && impact.Attraction is >= -100 and <= 100
        && impact.Obligation is >= -100 and <= 100
        && impact.Fear is >= -100 and <= 100
        && impact.Resentment is >= -100 and <= 100
        && impact.Rivalry is >= -100 and <= 100
        && impact.Compatibility is >= -200 and <= 200;

    private static void AddAgencyIssues(
        AuthoritativeCharacterProfile profile,
        string role,
        ICollection<ValidationIssue> issues)
    {
        if (profile.Condition.VitalStatus != CharacterVitalStatus.Alive)
        {
            issues.Add(new($"{role}_dead", $"Character action {role} is dead."));
        }

        if (profile.Condition.IsIncapacitated)
        {
            issues.Add(new($"{role}_incapacitated", $"Character action {role} is incapacitated."));
        }
    }

    private static bool CanExerciseAgency(
        AuthoritativeCharacterProfile profile,
        CampaignDate resolutionDate) =>
        profile.BirthDate.CompareTo(resolutionDate) <= 0
        && profile.Condition.VitalStatus == CharacterVitalStatus.Alive
        && !profile.Condition.IsIncapacitated;

    private int ActiveRetinueMembershipCount(EntityId leaderCharacterId) =>
        retinueMemberships.Values.Count(item =>
            item.IsActive && item.LeaderCharacterId == leaderCharacterId);

    private bool HasActiveRetinueMembership(EntityId leaderCharacterId, EntityId memberCharacterId) =>
        retinueMemberships.Values.Any(item =>
            item.IsActive
            && item.LeaderCharacterId == leaderCharacterId
            && item.MemberCharacterId == memberCharacterId);

    private int ActivePatronageCount(EntityId characterId) =>
        patronageBonds.Values.Count(item =>
            item.IsActive
            && (item.PatronCharacterId == characterId
                || item.BeneficiaryCharacterId == characterId));

    private bool HasActivePatronageBond(EntityId patronId, EntityId beneficiaryId) =>
        patronageBonds.Values.Any(item =>
            item.IsActive
            && item.PatronCharacterId == patronId
            && item.BeneficiaryCharacterId == beneficiaryId);

    private int ActiveEmploymentCount(EntityId employeeCharacterId) =>
        employmentTenures.Values.Count(item =>
            item.IsActive && item.EmployeeCharacterId == employeeCharacterId);

    private bool HasActiveEmploymentTenure(
        EntityId employeeCharacterId,
        ServicePrincipalReference employer,
        EntityId roleId) => employmentTenures.Values.Any(item =>
            item.IsActive
            && item.EmployeeCharacterId == employeeCharacterId
            && item.Employer == employer
            && item.RoleId == roleId);

    private static void ValidateSnapshotShape(CareerWorldSnapshot snapshot)
    {
        if (snapshot.ContractVersion != CareerContractVersions.Snapshot)
        {
            throw new SimulationValidationException(
                $"Unsupported career-world snapshot contract version {snapshot.ContractVersion}.");
        }

        if (snapshot.Proposals is null
            || snapshot.Retinues is null
            || snapshot.RetinueMemberships is null
            || snapshot.PatronageBonds is null
            || snapshot.Recommendations is null
            || snapshot.EmploymentTenures is null
            || snapshot.History is null
            || snapshot.Proposals.Any(item => item is null)
            || snapshot.Retinues.Any(item => item is null)
            || snapshot.RetinueMemberships.Any(item => item is null)
            || snapshot.PatronageBonds.Any(item => item is null)
            || snapshot.Recommendations.Any(item => item is null)
            || snapshot.EmploymentTenures.Any(item => item is null)
            || snapshot.History.Any(item => item is null))
        {
            throw new SimulationValidationException(
                "Career-world snapshot collections and entries cannot be null.");
        }
    }

    private void AddProposals(IReadOnlyList<CareerProposalState> source)
    {
        foreach (CareerProposalState proposal in source)
        {
            ValidateStateVersion(proposal.ContractVersion, "Career proposal", proposal.ProposalId);
            ValidateId(proposal.ProposalId, "Career proposal ID");
            ValidateId(proposal.SourceCommandId, $"Career proposal '{proposal.ProposalId}' source command ID");
            if (!Enum.IsDefined(proposal.Kind)
                || !Enum.IsDefined(proposal.Status))
            {
                throw new SimulationValidationException(
                    $"Career proposal '{proposal.ProposalId}' has an invalid kind or status.");
            }

            if (proposal.ProposalId != CareerIds.DeriveProposalId(
                    proposal.Kind,
                    proposal.CreatedDate,
                    proposal.SourceCommandId))
            {
                throw new SimulationValidationException(
                    $"Career proposal '{proposal.ProposalId}' does not have its exact deterministic ID.");
            }

            ValidateCharacterAtDate(
                proposal.ProposerCharacterId,
                proposal.CreatedDate,
                $"Career proposal '{proposal.ProposalId}' proposer");
            ValidateCharacterAtDate(
                proposal.RecipientCharacterId,
                proposal.CreatedDate,
                $"Career proposal '{proposal.ProposalId}' recipient");
            if (proposal.ProposerCharacterId == proposal.RecipientCharacterId)
            {
                throw new SimulationValidationException(
                    $"Career proposal '{proposal.ProposalId}' cannot be self-directed.");
            }

            ValidatePrincipalState(proposal.Principal, $"Career proposal '{proposal.ProposalId}' principal");
            ValidateRecordPoint(
                proposal.CreatedDate,
                proposal.CreatedTurnIndex,
                $"Career proposal '{proposal.ProposalId}' creation");
            ValidateProposalKindShape(proposal);
            ValidateProposalCompletionShape(proposal);
            if (!proposals.TryAdd(proposal.ProposalId, Clone(proposal)))
            {
                throw new SimulationValidationException(
                    $"Duplicate career proposal '{proposal.ProposalId}'.");
            }
        }
    }

    private void AddRetinues(IReadOnlyList<RetinueState> source)
    {
        foreach (RetinueState retinue in source)
        {
            ValidateStateVersion(retinue.ContractVersion, "Retinue", retinue.RetinueId);
            ValidateCharacter(retinue.LeaderCharacterId, $"Retinue '{retinue.RetinueId}' leader");
            if (retinue.RetinueId != CareerIds.DeriveRetinueId(retinue.LeaderCharacterId))
            {
                throw new SimulationValidationException(
                    $"Retinue '{retinue.RetinueId}' does not have its exact deterministic ID.");
            }

            if (!retinues.TryAdd(retinue.RetinueId, Clone(retinue)))
            {
                throw new SimulationValidationException($"Duplicate retinue '{retinue.RetinueId}'.");
            }
        }
    }

    private void AddRetinueMemberships(IReadOnlyList<RetinueMembershipState> source)
    {
        foreach (RetinueMembershipState membership in source)
        {
            ValidateStateVersion(
                membership.ContractVersion,
                "Retinue membership",
                membership.MembershipId);
            ValidateId(membership.SourceProposalId, "Retinue membership source proposal ID");
            if (membership.MembershipId
                != CareerIds.DeriveRetinueMembershipId(membership.SourceProposalId))
            {
                throw new SimulationValidationException(
                    $"Retinue membership '{membership.MembershipId}' does not have its exact deterministic ID.");
            }

            ValidateCharacterAtDate(
                membership.LeaderCharacterId,
                membership.StartDate,
                $"Retinue membership '{membership.MembershipId}' leader");
            ValidateCharacterAtDate(
                membership.MemberCharacterId,
                membership.StartDate,
                $"Retinue membership '{membership.MembershipId}' member");
            if (membership.LeaderCharacterId == membership.MemberCharacterId)
            {
                throw new SimulationValidationException(
                    $"Retinue membership '{membership.MembershipId}' cannot make a leader their own member.");
            }

            if (membership.RetinueId != CareerIds.DeriveRetinueId(membership.LeaderCharacterId))
            {
                throw new SimulationValidationException(
                    $"Retinue membership '{membership.MembershipId}' refers to the wrong retinue ID.");
            }

            ValidateServiceTimeline(
                membership.StartDate,
                membership.StartTurnIndex,
                membership.EndDate,
                membership.EndTurnIndex,
                membership.EndCommandId,
                membership.EndReason,
                [
                    CareerServiceEndReason.MemberLeft,
                    CareerServiceEndReason.LeaderDied,
                    CareerServiceEndReason.MemberDied,
                ],
                $"Retinue membership '{membership.MembershipId}'");
            if (!retinueMemberships.TryAdd(membership.MembershipId, Clone(membership)))
            {
                throw new SimulationValidationException(
                    $"Duplicate retinue membership '{membership.MembershipId}'.");
            }
        }
    }

    private void AddPatronageBonds(IReadOnlyList<PatronageBondState> source)
    {
        foreach (PatronageBondState bond in source)
        {
            ValidateStateVersion(bond.ContractVersion, "Patronage bond", bond.BondId);
            ValidateId(bond.SourceProposalId, "Patronage bond source proposal ID");
            if (bond.BondId != CareerIds.DerivePatronageBondId(bond.SourceProposalId))
            {
                throw new SimulationValidationException(
                    $"Patronage bond '{bond.BondId}' does not have its exact deterministic ID.");
            }

            ValidateCharacterAtDate(
                bond.PatronCharacterId,
                bond.StartDate,
                $"Patronage bond '{bond.BondId}' patron");
            ValidateCharacterAtDate(
                bond.BeneficiaryCharacterId,
                bond.StartDate,
                $"Patronage bond '{bond.BondId}' beneficiary");
            if (bond.PatronCharacterId == bond.BeneficiaryCharacterId)
            {
                throw new SimulationValidationException(
                    $"Patronage bond '{bond.BondId}' cannot be self-directed.");
            }

            ValidateServiceTimeline(
                bond.StartDate,
                bond.StartTurnIndex,
                bond.EndDate,
                bond.EndTurnIndex,
                bond.EndCommandId,
                bond.EndReason,
                [
                    CareerServiceEndReason.PatronEnded,
                    CareerServiceEndReason.BeneficiaryEnded,
                    CareerServiceEndReason.PatronDied,
                    CareerServiceEndReason.BeneficiaryDied,
                ],
                $"Patronage bond '{bond.BondId}'");
            if (!patronageBonds.TryAdd(bond.BondId, Clone(bond)))
            {
                throw new SimulationValidationException($"Duplicate patronage bond '{bond.BondId}'.");
            }
        }
    }

    private void AddRecommendations(IReadOnlyList<RecommendationRecord> source)
    {
        foreach (RecommendationRecord recommendation in source)
        {
            ValidateStateVersion(
                recommendation.ContractVersion,
                "Recommendation",
                recommendation.RecommendationId);
            ValidateId(recommendation.SourceCommandId, "Recommendation source command ID");
            if (recommendation.RecommendationId
                != CareerIds.DeriveRecommendationId(
                    recommendation.RecordedDate,
                    recommendation.SourceCommandId))
            {
                throw new SimulationValidationException(
                    $"Recommendation '{recommendation.RecommendationId}' does not have its exact deterministic ID.");
            }

            ValidateCharacterAtDate(
                recommendation.RecommenderCharacterId,
                recommendation.RecordedDate,
                $"Recommendation '{recommendation.RecommendationId}' recommender");
            ValidateCharacterAtDate(
                recommendation.BeneficiaryCharacterId,
                recommendation.RecordedDate,
                $"Recommendation '{recommendation.RecommendationId}' beneficiary");
            if (recommendation.RecommenderCharacterId == recommendation.BeneficiaryCharacterId)
            {
                throw new SimulationValidationException(
                    $"Recommendation '{recommendation.RecommendationId}' cannot be self-directed.");
            }

            ValidatePrincipalState(
                recommendation.Principal,
                $"Recommendation '{recommendation.RecommendationId}' principal");
            if (recommendation.Principal.Kind == ServicePrincipalKind.Character
                && recommendation.Principal.PrincipalId == recommendation.BeneficiaryCharacterId)
            {
                throw new SimulationValidationException(
                    $"Recommendation '{recommendation.RecommendationId}' cannot recommend a character to themself.");
            }

            if (recommendation.RecommendedRoleId is EntityId roleId && !roleId.IsValid)
            {
                throw new SimulationValidationException(
                    $"Recommendation '{recommendation.RecommendationId}' has an invalid role ID.");
            }

            ValidateRecordPoint(
                recommendation.RecordedDate,
                recommendation.RecordedTurnIndex,
                $"Recommendation '{recommendation.RecommendationId}' recording");
            if (!recommendations.TryAdd(recommendation.RecommendationId, Clone(recommendation)))
            {
                throw new SimulationValidationException(
                    $"Duplicate recommendation '{recommendation.RecommendationId}'.");
            }
        }
    }

    private void AddEmploymentTenures(IReadOnlyList<EmploymentTenure> source)
    {
        foreach (EmploymentTenure tenure in source)
        {
            ValidateStateVersion(tenure.ContractVersion, "Employment tenure", tenure.TenureId);
            ValidateId(tenure.SourceProposalId, "Employment tenure source proposal ID");
            if (tenure.TenureId != CareerIds.DeriveEmploymentTenureId(tenure.SourceProposalId))
            {
                throw new SimulationValidationException(
                    $"Employment tenure '{tenure.TenureId}' does not have its exact deterministic ID.");
            }

            ValidateCharacterAtDate(
                tenure.EmployeeCharacterId,
                tenure.StartDate,
                $"Employment tenure '{tenure.TenureId}' employee");
            ValidatePrincipalState(tenure.Employer, $"Employment tenure '{tenure.TenureId}' employer");
            ValidateId(tenure.RoleId, $"Employment tenure '{tenure.TenureId}' role ID");
            if (tenure.Employer.Kind == ServicePrincipalKind.Character
                && tenure.Employer.PrincipalId == tenure.EmployeeCharacterId)
            {
                throw new SimulationValidationException(
                    $"Employment tenure '{tenure.TenureId}' cannot employ a character under themself.");
            }

            ValidateServiceTimeline(
                tenure.StartDate,
                tenure.StartTurnIndex,
                tenure.EndDate,
                tenure.EndTurnIndex,
                tenure.EndCommandId,
                tenure.EndReason,
                [
                    CareerServiceEndReason.EmployeeLeft,
                    CareerServiceEndReason.EmployerEnded,
                    CareerServiceEndReason.EmployeeDied,
                    CareerServiceEndReason.EmployerDied,
                ],
                $"Employment tenure '{tenure.TenureId}'");
            if (!employmentTenures.TryAdd(tenure.TenureId, Clone(tenure)))
            {
                throw new SimulationValidationException(
                    $"Duplicate employment tenure '{tenure.TenureId}'.");
            }
        }
    }

    private void AddHistory(IReadOnlyList<CareerHistoryAggregate> source)
    {
        foreach (CareerHistoryAggregate aggregate in source)
        {
            ValidateStateVersion(
                aggregate.ContractVersion,
                "Career history aggregate",
                aggregate.CharacterId);
            ValidateCharacter(aggregate.CharacterId, "Career history character");
            long total;
            try
            {
                total = aggregate.TotalFoldedRecordCount;
            }
            catch (OverflowException exception)
            {
                throw new SimulationValidationException(
                    $"Career history for '{aggregate.CharacterId}' overflows: {exception.Message}");
            }

            if (aggregate.FoldedRetinueProposalCount < 0
                || aggregate.FoldedPatronageProposalCount < 0
                || aggregate.FoldedEmploymentProposalCount < 0
                || aggregate.FoldedRetinueMembershipCount < 0
                || aggregate.FoldedPatronageBondCount < 0
                || aggregate.FoldedRecommendationCount < 0
                || aggregate.FoldedEmploymentTenureCount < 0)
            {
                throw new SimulationValidationException(
                    $"Career history for '{aggregate.CharacterId}' has a negative counter.");
            }

            if (total == 0)
            {
                if (aggregate.EarliestDate is not null || aggregate.LatestDate is not null)
                {
                    throw new SimulationValidationException(
                        $"Empty career history for '{aggregate.CharacterId}' has dates.");
                }
            }
            else if (aggregate.EarliestDate is not CampaignDate earliest
                || aggregate.LatestDate is not CampaignDate latest
                || !earliest.IsValid
                || !latest.IsValid
                || earliest.CompareTo(latest) > 0
                || latest.CompareTo(calendar.Date) > 0)
            {
                throw new SimulationValidationException(
                    $"Career history for '{aggregate.CharacterId}' has inconsistent dates.");
            }
            else
            {
                _ = characters.TryGetCharacterProfile(
                    aggregate.CharacterId,
                    out AuthoritativeCharacterProfile? profile);
                if (profile!.BirthDate.CompareTo(earliest) > 0)
                {
                    throw new SimulationValidationException(
                        $"Career history for '{aggregate.CharacterId}' predates the character.");
                }
            }

            if (!history.TryAdd(aggregate.CharacterId, Clone(aggregate)))
            {
                throw new SimulationValidationException(
                    $"Duplicate career history for '{aggregate.CharacterId}'.");
            }
        }
    }

    private void ValidateCrossRecordState()
    {
        foreach (RetinueMembershipState membership in retinueMemberships.Values)
        {
            if (!retinues.TryGetValue(membership.RetinueId, out RetinueState? retinue)
                || retinue.LeaderCharacterId != membership.LeaderCharacterId)
            {
                throw new SimulationValidationException(
                    $"Retinue membership '{membership.MembershipId}' has a dangling or mismatched retinue.");
            }
        }

        EnsureUniqueActive(
            proposals.Values,
            item => item.Status == CareerProposalStatus.Active,
            item => string.Join(
                "|",
                (int)item.Kind,
                item.ProposerCharacterId.Value,
                item.RecipientCharacterId.Value,
                (int)item.Principal.Kind,
                item.Principal.PrincipalId.Value,
                item.ProposedRoleId?.Value ?? string.Empty),
            "equivalent career proposal");
        EnsureUniqueActive(
            retinueMemberships.Values,
            item => item.IsActive,
            item => $"{item.LeaderCharacterId.Value}|{item.MemberCharacterId.Value}",
            "retinue leader/member pair");
        EnsureUniqueActive(
            patronageBonds.Values,
            item => item.IsActive,
            item => $"{item.PatronCharacterId.Value}|{item.BeneficiaryCharacterId.Value}",
            "patronage pair");
        EnsureUniqueActive(
            employmentTenures.Values,
            item => item.IsActive,
            item => string.Join(
                "|",
                item.EmployeeCharacterId.Value,
                (int)item.Employer.Kind,
                item.Employer.PrincipalId.Value,
                item.RoleId.Value),
            "employment employee/employer/role tuple");
    }

    private void ValidateRetentionBounds()
    {
        foreach (IGrouping<EntityId, CareerProposalState> group in proposals.Values
            .Where(item => item.Status == CareerProposalStatus.Active)
            .GroupBy(item => item.RecipientCharacterId))
        {
            if (group.Count() > CareerLimits.ActiveProposalsPerRecipient)
            {
                throw new SimulationValidationException(
                    $"Character '{group.Key}' has more than eight active career proposals.");
            }
        }

        foreach (IGrouping<EntityId, RetinueMembershipState> group in retinueMemberships.Values
            .Where(item => item.IsActive)
            .GroupBy(item => item.LeaderCharacterId))
        {
            if (group.Count() > CareerLimits.ActiveMembershipsPerRetinue)
            {
                throw new SimulationValidationException(
                    $"Retinue leader '{group.Key}' has more than 64 active members.");
            }
        }

        Dictionary<EntityId, int> activePatronage = [];
        foreach (PatronageBondState bond in patronageBonds.Values.Where(item => item.IsActive))
        {
            IncrementBounded(
                activePatronage,
                bond.PatronCharacterId,
                CareerLimits.ActivePatronageBondsPerCharacter,
                "active patronage bonds");
            IncrementBounded(
                activePatronage,
                bond.BeneficiaryCharacterId,
                CareerLimits.ActivePatronageBondsPerCharacter,
                "active patronage bonds");
        }

        foreach (IGrouping<EntityId, EmploymentTenure> group in employmentTenures.Values
            .Where(item => item.IsActive)
            .GroupBy(item => item.EmployeeCharacterId))
        {
            if (group.Count() > CareerLimits.ActiveEmploymentTenuresPerEmployee)
            {
                throw new SimulationValidationException(
                    $"Employee '{group.Key}' has more than eight active employment tenures.");
            }
        }

        ValidateCompletedRetention(
            proposals.Values.Where(item => item.Status != CareerProposalStatus.Active),
            item => GetInvolvedCharacters(item),
            item => $"proposal.{(int)item.Kind}",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            "completed career proposals");
        ValidateCompletedRetention(
            retinueMemberships.Values.Where(item => !item.IsActive),
            item => GetInvolvedCharacters(item),
            _ => "retinue-membership",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            "completed retinue memberships");
        ValidateCompletedRetention(
            patronageBonds.Values.Where(item => !item.IsActive),
            item => GetInvolvedCharacters(item),
            _ => "patronage-bond",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            "completed patronage bonds");
        ValidateCompletedRetention(
            employmentTenures.Values.Where(item => !item.IsActive),
            item => GetInvolvedCharacters(item),
            _ => "employment-tenure",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            "completed employment tenures");
        ValidateCompletedRetention(
            recommendations.Values,
            item => GetInvolvedCharacters(item),
            _ => "recommendation",
            CareerLimits.RecommendationsPerCharacter,
            "recommendations");
    }

    private static void ValidateCompletedRetention<T>(
        IEnumerable<T> records,
        Func<T, IReadOnlyList<EntityId>> involvedCharacters,
        Func<T, string> category,
        int limit,
        string description)
    {
        Dictionary<(EntityId CharacterId, string Category), int> counts = [];
        foreach (T record in records)
        {
            foreach (EntityId characterId in involvedCharacters(record))
            {
                (EntityId, string) key = (characterId, category(record));
                int next = counts.TryGetValue(key, out int current) ? checked(current + 1) : 1;
                if (next > limit)
                {
                    throw new SimulationValidationException(
                        $"Character '{characterId}' exceeds retained {description}.");
                }

                counts[key] = next;
            }
        }
    }

    private static void IncrementBounded(
        IDictionary<EntityId, int> counts,
        EntityId characterId,
        int limit,
        string description)
    {
        int next = counts.TryGetValue(characterId, out int current) ? checked(current + 1) : 1;
        if (next > limit)
        {
            throw new SimulationValidationException(
                $"Character '{characterId}' exceeds {description}.");
        }

        counts[characterId] = next;
    }

    private void ValidateProposalKindShape(CareerProposalState proposal)
    {
        if (proposal.Kind is CareerProposalKind.RetinueInvitation or CareerProposalKind.PatronageOffer)
        {
            if (proposal.Principal.Kind != ServicePrincipalKind.Character
                || proposal.Principal.PrincipalId != proposal.ProposerCharacterId
                || proposal.ProposedRoleId is not null)
            {
                throw new SimulationValidationException(
                    $"Career proposal '{proposal.ProposalId}' violates retinue/patronage layer separation.");
            }
        }
        else if (proposal.ProposedRoleId is not EntityId roleId || !roleId.IsValid)
        {
            throw new SimulationValidationException(
                $"Employment proposal '{proposal.ProposalId}' requires a valid role ID.");
        }

        if (proposal.Kind == CareerProposalKind.EmploymentOffer
            && proposal.Principal.Kind == ServicePrincipalKind.Character
            && proposal.Principal.PrincipalId != proposal.ProposerCharacterId)
        {
            throw new SimulationValidationException(
                $"Employment proposal '{proposal.ProposalId}' has a mismatched character principal.");
        }
    }

    private void ValidateProposalCompletionShape(CareerProposalState proposal)
    {
        if (proposal.Status == CareerProposalStatus.Active)
        {
            if (proposal.ResolutionDate is not null
                || proposal.ResolutionTurnIndex is not null
                || proposal.ResolutionCommandId is not null)
            {
                throw new SimulationValidationException(
                    $"Active career proposal '{proposal.ProposalId}' has completion data.");
            }

            return;
        }

        if (proposal.ResolutionDate is not CampaignDate resolutionDate
            || proposal.ResolutionTurnIndex is not long resolutionTurn
            || proposal.ResolutionCommandId is not EntityId resolutionCommand
            || !resolutionCommand.IsValid
            || resolutionDate.CompareTo(proposal.CreatedDate) < 0
            || resolutionTurn < proposal.CreatedTurnIndex)
        {
            throw new SimulationValidationException(
                $"Completed career proposal '{proposal.ProposalId}' has inconsistent completion data.");
        }

        ValidateRecordPoint(
            resolutionDate,
            resolutionTurn,
            $"Career proposal '{proposal.ProposalId}' resolution");
    }

    private void ValidateServiceTimeline<TReason>(
        CampaignDate startDate,
        long startTurnIndex,
        CampaignDate? endDate,
        long? endTurnIndex,
        EntityId? endCommandId,
        TReason? endReason,
        IReadOnlyList<TReason> allowedReasons,
        string description)
        where TReason : struct, Enum
    {
        ValidateRecordPoint(startDate, startTurnIndex, $"{description} start");
        if (endDate is null)
        {
            if (endTurnIndex is not null || endCommandId is not null || endReason is not null)
            {
                throw new SimulationValidationException(
                    $"Active {description} has completion data.");
            }

            return;
        }

        if (endTurnIndex is not long completedTurn
            || endCommandId is not EntityId commandId
            || !commandId.IsValid
            || endReason is not TReason reason
            || !Enum.IsDefined(reason)
            || !allowedReasons.Contains(reason)
            || endDate.Value.CompareTo(startDate) < 0
            || completedTurn < startTurnIndex)
        {
            throw new SimulationValidationException($"Completed {description} is inconsistent.");
        }

        ValidateRecordPoint(endDate.Value, completedTurn, $"{description} completion");
    }

    private void ValidateRecordPoint(CampaignDate date, long turnIndex, string description)
    {
        if (!date.IsValid
            || date.CompareTo(calendar.Date) > 0
            || turnIndex < 0
            || turnIndex > calendar.TurnIndex)
        {
            throw new SimulationValidationException($"{description} date or turn is invalid.");
        }
    }

    private void ValidatePrincipalState(ServicePrincipalReference? principal, string description)
    {
        if (principal is null
            || !Enum.IsDefined(principal.Kind)
            || !principal.PrincipalId.IsValid)
        {
            throw new SimulationValidationException($"{description} is invalid.");
        }

        switch (principal.Kind)
        {
            case ServicePrincipalKind.Character:
                ValidateCharacter(principal.PrincipalId, description);
                break;
            case ServicePrincipalKind.Household:
                if (!characters.TryGetHousehold(principal.PrincipalId, out _))
                {
                    throw new SimulationValidationException(
                        $"{description} household '{principal.PrincipalId}' does not exist.");
                }

                break;
            default:
                throw new SimulationValidationException($"{description} has an unsupported kind.");
        }
    }

    private void ValidateCharacter(EntityId characterId, string description)
    {
        ValidateId(characterId, $"{description} ID");
        if (!characters.TryGetCharacterProfile(characterId, out _))
        {
            throw new SimulationValidationException(
                $"{description} character '{characterId}' does not exist.");
        }
    }

    private void ValidateCharacterAtDate(
        EntityId characterId,
        CampaignDate date,
        string description)
    {
        ValidateCharacter(characterId, description);
        if (!date.IsValid)
        {
            throw new SimulationValidationException($"{description} date is invalid.");
        }

        _ = characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile);
        if (profile!.BirthDate.CompareTo(date) > 0)
        {
            throw new SimulationValidationException($"{description} is not born on the record date.");
        }
    }

    private static void EnsureUniqueActive<T>(
        IEnumerable<T> records,
        Func<T, bool> isActive,
        Func<T, string> keySelector,
        string description)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (T record in records.Where(isActive))
        {
            if (!keys.Add(keySelector(record)))
            {
                throw new SimulationValidationException(
                    $"Career state contains a duplicate active {description}.");
            }
        }
    }

    private CharacterCareerWorldState CreateValidatedCandidate(
        ICharacterActionOutcome outcome,
        CampaignDate resolutionDate,
        long authoritativeTurnIndex)
    {
        CareerWorldSnapshot current = CaptureSnapshot();
        List<CareerProposalState> nextProposals = current.Proposals.Select(Clone).ToList();
        List<RetinueState> nextRetinues = current.Retinues.Select(Clone).ToList();
        List<RetinueMembershipState> nextMemberships = current.RetinueMemberships.Select(Clone).ToList();
        List<PatronageBondState> nextBonds = current.PatronageBonds.Select(Clone).ToList();
        List<RecommendationRecord> nextRecommendations = current.Recommendations.Select(Clone).ToList();
        List<EmploymentTenure> nextTenures = current.EmploymentTenures.Select(Clone).ToList();
        List<CareerHistoryAggregate> nextHistory = current.History.Select(Clone).ToList();

        switch (outcome)
        {
            case CareerProposalCreatedOutcome created:
                nextProposals.Add(Clone(created.Proposal));
                break;
            case CareerProposalRefusedOutcome refused:
                ReplaceById(
                    nextProposals,
                    refused.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                break;
            case CareerProposalWithdrawnOutcome withdrawn:
                ReplaceById(
                    nextProposals,
                    withdrawn.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                break;
            case CareerProposalInvalidatedOutcome invalidated:
                ReplaceById(
                    nextProposals,
                    invalidated.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                break;
            case RetinueInvitationAcceptedOutcome accepted:
                ReplaceById(
                    nextProposals,
                    accepted.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                UpsertIdentical(
                    nextRetinues,
                    accepted.Retinue,
                    item => item.RetinueId,
                    "retinue");
                nextMemberships.Add(Clone(accepted.Membership));
                break;
            case RetinueMembershipEndedOutcome ended:
                ReplaceById(
                    nextMemberships,
                    ended.Membership,
                    item => item.MembershipId,
                    "retinue membership");
                break;
            case PatronageOfferAcceptedOutcome accepted:
                ReplaceById(
                    nextProposals,
                    accepted.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                nextBonds.Add(Clone(accepted.Bond));
                break;
            case PatronageBondEndedOutcome ended:
                ReplaceById(nextBonds, ended.Bond, item => item.BondId, "patronage bond");
                break;
            case RecommendationRecordedOutcome recorded:
                nextRecommendations.Add(Clone(recorded.Recommendation));
                break;
            case EmploymentOfferAcceptedOutcome accepted:
                ReplaceById(
                    nextProposals,
                    accepted.Proposal,
                    item => item.ProposalId,
                    "career proposal");
                nextTenures.Add(Clone(accepted.Tenure));
                break;
            case EmploymentTenureEndedOutcome ended:
                ReplaceById(nextTenures, ended.Tenure, item => item.TenureId, "employment tenure");
                break;
            default:
                throw new SimulationValidationException(
                    "Character action outcome type is null or unsupported.");
        }

        CareerWorldSnapshot candidate = new(
            CareerContractVersions.Snapshot,
            nextProposals,
            nextRetinues,
            nextMemberships,
            nextBonds,
            nextRecommendations,
            nextTenures,
            nextHistory);
        try
        {
            candidate = NormalizeRetention(candidate);
        }
        catch (OverflowException exception)
        {
            throw new SimulationValidationException(
                $"Career history counters exceeded their supported range: {exception.Message}");
        }

        CampaignCalendar candidateCalendar = new(
            Max(calendar.Date, resolutionDate),
            Math.Max(calendar.TurnIndex, authoritativeTurnIndex));
        return new CharacterCareerWorldState(candidate, characters, candidateCalendar);
    }

    private static void ReplaceById<T>(
        IList<T> target,
        T replacement,
        Func<T, EntityId> idSelector,
        string description)
    {
        EntityId id = idSelector(replacement);
        int index = target.ToList().FindIndex(item => idSelector(item) == id);
        if (index < 0)
        {
            throw new SimulationValidationException(
                $"Cannot replace unknown {description} '{id}'.");
        }

        target[index] = replacement;
    }

    private static void UpsertIdentical<T>(
        IList<T> target,
        T value,
        Func<T, EntityId> idSelector,
        string description)
    {
        EntityId id = idSelector(value);
        T? existing = target.SingleOrDefault(item => idSelector(item) == id);
        if (existing is null)
        {
            target.Add(value);
            return;
        }

        string existingJson = JsonSerializer.Serialize(existing, SimulationJson.CreateOptions());
        string incomingJson = JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
        if (!StringComparer.Ordinal.Equals(existingJson, incomingJson))
        {
            throw new SimulationValidationException(
                $"Existing {description} '{id}' conflicts with the planned outcome.");
        }
    }

    private static CareerWorldSnapshot NormalizeRetention(CareerWorldSnapshot snapshot)
    {
        Dictionary<EntityId, CareerHistoryAggregate> aggregates = snapshot.History
            .ToDictionary(item => item.CharacterId, Clone);

        List<CareerProposalState> proposals = RetainRecords(
            snapshot.Proposals,
            item => item.Status != CareerProposalStatus.Active,
            item => item.ResolutionTurnIndex!.Value,
            item => item.ResolutionDate!.Value,
            item => item.ProposalId,
            GetInvolvedCharacters,
            item => $"proposal.{(int)item.Kind}",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            item => Fold(
                aggregates,
                GetInvolvedCharacters(item),
                ProposalHistoryCategory(item.Kind),
                item.ResolutionDate!.Value));
        List<RetinueMembershipState> memberships = RetainRecords(
            snapshot.RetinueMemberships,
            item => !item.IsActive,
            item => item.EndTurnIndex!.Value,
            item => item.EndDate!.Value,
            item => item.MembershipId,
            GetInvolvedCharacters,
            _ => "retinue-membership",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            item => Fold(
                aggregates,
                GetInvolvedCharacters(item),
                HistoryCategory.RetinueMembership,
                item.EndDate!.Value));
        List<PatronageBondState> bonds = RetainRecords(
            snapshot.PatronageBonds,
            item => !item.IsActive,
            item => item.EndTurnIndex!.Value,
            item => item.EndDate!.Value,
            item => item.BondId,
            GetInvolvedCharacters,
            _ => "patronage-bond",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            item => Fold(
                aggregates,
                GetInvolvedCharacters(item),
                HistoryCategory.PatronageBond,
                item.EndDate!.Value));
        List<RecommendationRecord> recommendations = RetainRecords(
            snapshot.Recommendations,
            _ => true,
            item => item.RecordedTurnIndex,
            item => item.RecordedDate,
            item => item.RecommendationId,
            GetInvolvedCharacters,
            _ => "recommendation",
            CareerLimits.RecommendationsPerCharacter,
            item => Fold(
                aggregates,
                GetInvolvedCharacters(item),
                HistoryCategory.Recommendation,
                item.RecordedDate));
        List<EmploymentTenure> tenures = RetainRecords(
            snapshot.EmploymentTenures,
            item => !item.IsActive,
            item => item.EndTurnIndex!.Value,
            item => item.EndDate!.Value,
            item => item.TenureId,
            GetInvolvedCharacters,
            _ => "employment-tenure",
            CareerLimits.CompletedRecordsPerCategoryPerCharacter,
            item => Fold(
                aggregates,
                GetInvolvedCharacters(item),
                HistoryCategory.EmploymentTenure,
                item.EndDate!.Value));

        return new CareerWorldSnapshot(
            CareerContractVersions.Snapshot,
            proposals.OrderBy(item => item.ProposalId).Select(Clone).ToArray(),
            snapshot.Retinues.OrderBy(item => item.RetinueId).Select(Clone).ToArray(),
            memberships.OrderBy(item => item.MembershipId).Select(Clone).ToArray(),
            bonds.OrderBy(item => item.BondId).Select(Clone).ToArray(),
            recommendations.OrderBy(item => item.RecommendationId).Select(Clone).ToArray(),
            tenures.OrderBy(item => item.TenureId).Select(Clone).ToArray(),
            aggregates.Values.OrderBy(item => item.CharacterId).Select(Clone).ToArray());
    }

    private static List<T> RetainRecords<T>(
        IEnumerable<T> source,
        Func<T, bool> isCompleted,
        Func<T, long> completedTurn,
        Func<T, CampaignDate> completedDate,
        Func<T, EntityId> id,
        Func<T, IReadOnlyList<EntityId>> involvedCharacters,
        Func<T, string> category,
        int limit,
        Action<T> onEvicted)
    {
        List<T> retained = source.Where(item => !isCompleted(item)).ToList();
        Dictionary<(EntityId CharacterId, string Category), int> counts = [];
        IEnumerable<T> completed = source
            .Where(isCompleted)
            .OrderByDescending(completedTurn)
            .ThenByDescending(completedDate)
            .ThenBy(id);
        foreach (T record in completed)
        {
            string recordCategory = category(record);
            (EntityId CharacterId, string Category)[] keys = involvedCharacters(record)
                .Select(characterId => (characterId, recordCategory))
                .Distinct()
                .ToArray();
            if (keys.Any(key => counts.TryGetValue(key, out int count) && count >= limit))
            {
                onEvicted(record);
                continue;
            }

            retained.Add(record);
            foreach ((EntityId CharacterId, string Category) key in keys)
            {
                counts[key] = counts.TryGetValue(key, out int count)
                    ? checked(count + 1)
                    : 1;
            }
        }

        return retained;
    }

    private static void Fold(
        IDictionary<EntityId, CareerHistoryAggregate> aggregates,
        IReadOnlyList<EntityId> characters,
        HistoryCategory category,
        CampaignDate date)
    {
        foreach (EntityId characterId in characters.Distinct())
        {
            CareerHistoryAggregate current = aggregates.TryGetValue(
                characterId,
                out CareerHistoryAggregate? existing)
                ? existing
                : CareerHistoryAggregate.Empty(characterId);
            CareerHistoryAggregate updated = category switch
            {
                HistoryCategory.RetinueProposal => current with
                {
                    FoldedRetinueProposalCount = checked(current.FoldedRetinueProposalCount + 1),
                },
                HistoryCategory.PatronageProposal => current with
                {
                    FoldedPatronageProposalCount = checked(current.FoldedPatronageProposalCount + 1),
                },
                HistoryCategory.EmploymentProposal => current with
                {
                    FoldedEmploymentProposalCount = checked(current.FoldedEmploymentProposalCount + 1),
                },
                HistoryCategory.RetinueMembership => current with
                {
                    FoldedRetinueMembershipCount = checked(current.FoldedRetinueMembershipCount + 1),
                },
                HistoryCategory.PatronageBond => current with
                {
                    FoldedPatronageBondCount = checked(current.FoldedPatronageBondCount + 1),
                },
                HistoryCategory.Recommendation => current with
                {
                    FoldedRecommendationCount = checked(current.FoldedRecommendationCount + 1),
                },
                HistoryCategory.EmploymentTenure => current with
                {
                    FoldedEmploymentTenureCount = checked(current.FoldedEmploymentTenureCount + 1),
                },
                _ => throw new SimulationValidationException("Unsupported career history category."),
            };
            aggregates[characterId] = updated with
            {
                EarliestDate = Min(current.EarliestDate, date),
                LatestDate = Max(current.LatestDate, date),
            };
        }
    }

    private void ReplaceFrom(CharacterCareerWorldState source)
    {
        CareerWorldSnapshot snapshot = source.CaptureSnapshot();
        proposals.Clear();
        retinues.Clear();
        retinueMemberships.Clear();
        patronageBonds.Clear();
        recommendations.Clear();
        employmentTenures.Clear();
        history.Clear();
        foreach (CareerProposalState item in snapshot.Proposals)
        {
            proposals.Add(item.ProposalId, Clone(item));
        }

        foreach (RetinueState item in snapshot.Retinues)
        {
            retinues.Add(item.RetinueId, Clone(item));
        }

        foreach (RetinueMembershipState item in snapshot.RetinueMemberships)
        {
            retinueMemberships.Add(item.MembershipId, Clone(item));
        }

        foreach (PatronageBondState item in snapshot.PatronageBonds)
        {
            patronageBonds.Add(item.BondId, Clone(item));
        }

        foreach (RecommendationRecord item in snapshot.Recommendations)
        {
            recommendations.Add(item.RecommendationId, Clone(item));
        }

        foreach (EmploymentTenure item in snapshot.EmploymentTenures)
        {
            employmentTenures.Add(item.TenureId, Clone(item));
        }

        foreach (CareerHistoryAggregate item in snapshot.History)
        {
            history.Add(item.CharacterId, Clone(item));
        }

        calendar = source.calendar;
    }

    private static IReadOnlyList<EntityId> GetInvolvedCharacters(CareerProposalState proposal) =>
        WithCharacterPrincipal(
            [proposal.ProposerCharacterId, proposal.RecipientCharacterId],
            proposal.Principal);

    private static IReadOnlyList<EntityId> GetInvolvedCharacters(
        RetinueMembershipState membership) =>
        [membership.LeaderCharacterId, membership.MemberCharacterId];

    private static IReadOnlyList<EntityId> GetInvolvedCharacters(PatronageBondState bond) =>
        [bond.PatronCharacterId, bond.BeneficiaryCharacterId];

    private static IReadOnlyList<EntityId> GetInvolvedCharacters(
        RecommendationRecord recommendation) => WithCharacterPrincipal(
        [recommendation.RecommenderCharacterId, recommendation.BeneficiaryCharacterId],
        recommendation.Principal);

    private static IReadOnlyList<EntityId> GetInvolvedCharacters(EmploymentTenure tenure) =>
        WithCharacterPrincipal([tenure.EmployeeCharacterId], tenure.Employer);

    private static IReadOnlyList<EntityId> WithCharacterPrincipal(
        IEnumerable<EntityId> characters,
        ServicePrincipalReference principal)
    {
        IEnumerable<EntityId> all = principal.Kind == ServicePrincipalKind.Character
            ? characters.Append(principal.PrincipalId)
            : characters;
        return all.Distinct().Order().ToArray();
    }

    private static HistoryCategory ProposalHistoryCategory(CareerProposalKind kind) => kind switch
    {
        CareerProposalKind.RetinueInvitation => HistoryCategory.RetinueProposal,
        CareerProposalKind.PatronageOffer => HistoryCategory.PatronageProposal,
        CareerProposalKind.EmploymentOffer => HistoryCategory.EmploymentProposal,
        _ => throw new SimulationValidationException("Unsupported career proposal history kind."),
    };

    private static void ThrowIfInvalid(CommandValidationResult validation)
    {
        if (!validation.IsValid)
        {
            ThrowIfInvalid(validation.Issues);
        }
    }

    private static void ThrowIfInvalid(IEnumerable<ValidationIssue> issues)
    {
        ValidationIssue[] materialized = issues.ToArray();
        if (materialized.Length != 0)
        {
            throw new SimulationValidationException(
                string.Join("; ", materialized.Select(issue => issue.Message)));
        }
    }

    private static void ValidateStateVersion(int version, string description, EntityId id)
    {
        if (version != CareerContractVersions.State)
        {
            throw new SimulationValidationException(
                $"Unsupported {description} contract version {version} for '{id}'.");
        }
    }

    private static void ValidateId(EntityId id, string description)
    {
        if (!id.IsValid)
        {
            throw new SimulationValidationException($"{description} is invalid.");
        }
    }

    private static CampaignDate Max(CampaignDate left, CampaignDate right) =>
        left.CompareTo(right) >= 0 ? left : right;

    private static CampaignDate? Min(CampaignDate? left, CampaignDate right) =>
        left is CampaignDate value && value.CompareTo(right) <= 0 ? value : right;

    private static CampaignDate? Max(CampaignDate? left, CampaignDate right) =>
        left is CampaignDate value && value.CompareTo(right) >= 0 ? value : right;

    private static ServicePrincipalReference Clone(ServicePrincipalReference principal) =>
        principal with { };

    private static CareerProposalState Clone(CareerProposalState proposal) => proposal with
    {
        Principal = Clone(proposal.Principal),
    };

    private static RetinueState Clone(RetinueState retinue) => retinue with { };

    private static RetinueMembershipState Clone(RetinueMembershipState membership) =>
        membership with { };

    private static PatronageBondState Clone(PatronageBondState bond) => bond with { };

    private static RecommendationRecord Clone(RecommendationRecord recommendation) =>
        recommendation with
        {
            Principal = Clone(recommendation.Principal),
        };

    private static EmploymentTenure Clone(EmploymentTenure tenure) => tenure with
    {
        Employer = Clone(tenure.Employer),
    };

    private static CareerHistoryAggregate Clone(CareerHistoryAggregate aggregate) =>
        aggregate with { };

    private static RelationshipMemoryConsequenceSpecification Clone(
        RelationshipMemoryConsequenceSpecification consequence) => consequence with
        {
            Impact = consequence.Impact with { },
            WitnessIds = consequence.WitnessIds.ToArray(),
        };

    private static ICharacterAction Clone(ICharacterAction action) => action switch
    {
        RetinueInviteAction value => value with { },
        RespondToRetinueInvitationAction value => value with { },
        LeaveRetinueAction value => value with { },
        PatronageOfferAction value => value with { },
        RespondToPatronageOfferAction value => value with { },
        EndPatronageAction value => value with { },
        MakeRecommendationAction value => value with { Principal = Clone(value.Principal) },
        EmploymentOfferAction value => value with { Employer = Clone(value.Employer) },
        RespondToEmploymentOfferAction value => value with { },
        EndEmploymentAction value => value with { },
        WithdrawCareerProposalAction value => value with { },
        _ => throw new SimulationValidationException("Unsupported character action type."),
    };

    private static ICharacterActionOutcome Clone(ICharacterActionOutcome outcome) => outcome switch
    {
        CareerProposalCreatedOutcome value =>
            new CareerProposalCreatedOutcome(Clone(value.Proposal)),
        CareerProposalRefusedOutcome value =>
            new CareerProposalRefusedOutcome(Clone(value.Proposal)),
        CareerProposalWithdrawnOutcome value =>
            new CareerProposalWithdrawnOutcome(Clone(value.Proposal)),
        CareerProposalInvalidatedOutcome value =>
            new CareerProposalInvalidatedOutcome(Clone(value.Proposal)),
        RetinueInvitationAcceptedOutcome value => new RetinueInvitationAcceptedOutcome(
            Clone(value.Proposal),
            Clone(value.Retinue),
            Clone(value.Membership)),
        RetinueMembershipEndedOutcome value =>
            new RetinueMembershipEndedOutcome(Clone(value.Membership)),
        PatronageOfferAcceptedOutcome value => new PatronageOfferAcceptedOutcome(
            Clone(value.Proposal),
            Clone(value.Bond)),
        PatronageBondEndedOutcome value =>
            new PatronageBondEndedOutcome(Clone(value.Bond)),
        RecommendationRecordedOutcome value =>
            new RecommendationRecordedOutcome(Clone(value.Recommendation)),
        EmploymentOfferAcceptedOutcome value => new EmploymentOfferAcceptedOutcome(
            Clone(value.Proposal),
            Clone(value.Tenure)),
        EmploymentTenureEndedOutcome value =>
            new EmploymentTenureEndedOutcome(Clone(value.Tenure)),
        _ => throw new SimulationValidationException("Unsupported character action outcome type."),
    };

    private enum HistoryCategory
    {
        RetinueProposal,
        PatronageProposal,
        EmploymentProposal,
        RetinueMembership,
        PatronageBond,
        Recommendation,
        EmploymentTenure,
    }
}

internal sealed record CharacterCareerWorldUpdatePlan(CharacterCareerWorldState Candidate);

internal sealed record CharacterCareerDeathPlan(
    CharacterCareerDeathChangeSet Changes,
    CharacterCareerWorldUpdatePlan CareerPlan);
