using System.Text.Json;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterCareerTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly CampaignCalendar Calendar = new(Date, 10);
    private static readonly EntityId Household = new("household:career/test");
    private static readonly EntityId Role = new("role:career/advisor");
    private readonly ITestOutputHelper output;

    public CharacterCareerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void StableIdsUseVersionedLengthFramingAndRemainLayerSpecific()
    {
        EntityId actor = Character(0);
        EntityId proposal = CareerIds.DeriveProposalId(
            CareerProposalKind.RetinueInvitation,
            Date,
            new EntityId("command:career/golden"));

        Assert.Equal(
            "career_proposal:sha256/bd59f285a2a354ffbea6273071531138b82d39b019d9c64db896ef7225aff018",
            proposal.Value);
        Assert.Equal(
            "retinue:sha256/bb1b58fb8d90f4020f343d456ebab7e6863592ec69fd65b1cd93ab1182daef87",
            CareerIds.DeriveRetinueId(actor).Value);
        Assert.Equal(
            "retinue_membership:sha256/8df98cc6ca251085239285650822be42f0329a8d17899f81d45f48523cd857b4",
            CareerIds.DeriveRetinueMembershipId(proposal).Value);
        Assert.NotEqual(
            CareerIds.DerivePatronageBondId(proposal).Value,
            CareerIds.DeriveEmploymentTenureId(proposal).Value);
        Assert.NotEqual(
            CareerIds.DeriveProposalId(
                CareerProposalKind.RetinueInvitation,
                Date,
                new EntityId("command:career/golden")),
            CareerIds.DeriveProposalId(
                CareerProposalKind.PatronageOffer,
                Date,
                new EntityId("command:career/golden")));
        Assert.NotEqual(
            CareerIds.DeriveCharacterActionEventId(Date, new EntityId("command:career/golden")),
            CareerIds.DeriveCharacterActionEventId(Date.AddDays(1), new EntityId("command:career/golden")));
        Assert.Throws<ArgumentException>(() => CareerIds.DeriveRetinueId(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => CareerIds.DeriveRelationshipConsequenceId(
            CareerIds.DeriveCharacterActionEventId(Date, new EntityId("command:career/golden")),
            -1));
    }

    [Fact]
    public void ConstructionCanonicalizesShuffledInputAndQueriesAreDefensive()
    {
        CharacterWorldState characters = CreateCharacters(4);
        CharacterCareerWorldState state = NewState(characters);
        PlannedAction invitation = Apply(
            state,
            Character(0),
            new RetinueInviteAction(Character(1)),
            "canonical-invite");
        Apply(
            state,
            Character(2),
            new PatronageOfferAction(Character(3)),
            "canonical-patronage");
        CareerWorldSnapshot snapshot = state.CaptureSnapshot();
        CareerWorldSnapshot shuffled = snapshot with
        {
            Proposals = snapshot.Proposals.Reverse().ToArray(),
            Retinues = snapshot.Retinues.Reverse().ToArray(),
            History = snapshot.History.Reverse().ToArray(),
        };
        CharacterCareerWorldState restored = new(shuffled, characters, Calendar);

        Assert.Equal(Serialize(snapshot), Serialize(restored.CaptureSnapshot()));
        CareerProposalState[] queried = Assert.IsType<CareerProposalState[]>(restored.Proposals);
        queried[0] = queried[0] with { Status = CareerProposalStatus.Withdrawn };
        CareerProposalState[] captured = Assert.IsType<CareerProposalState[]>(restored.CaptureSnapshot().Proposals);
        captured[0] = captured[0] with { Status = CareerProposalStatus.Refused };
        Assert.All(restored.Proposals, item => Assert.Equal(CareerProposalStatus.Active, item.Status));

        Assert.True(restored.TryGetProposal(
            Assert.IsType<CareerProposalCreatedOutcome>(invitation.Payload.Outcome).Proposal.ProposalId,
            out CareerProposalState? proposal));
        Assert.Equal(CareerProposalStatus.Active, proposal.Status);
    }

    [Fact]
    public void CampaignCalendarUpdateAcceptsOnlyMonotonicValidState()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(2));
        state.UpdateCampaignCalendar(new CampaignCalendar(Date.AddDays(3), Calendar.TurnIndex + 1));

        Assert.True(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(new RetinueInviteAction(Character(1))),
            Date.AddDays(3),
            Calendar.TurnIndex + 1).IsValid);
        Assert.Throws<SimulationValidationException>(() => state.UpdateCampaignCalendar(
            new CampaignCalendar(Date, Calendar.TurnIndex + 1)));
        Assert.Throws<SimulationValidationException>(() => state.UpdateCampaignCalendar(
            new CampaignCalendar(Date.AddDays(4), Calendar.TurnIndex)));
        Assert.Throws<SimulationValidationException>(() => state.UpdateCampaignCalendar(default));
    }

    [Fact]
    public void ConstructionRejectsMalformedDanglingSelfDuplicateDateAndCompletionState()
    {
        CharacterWorldState characters = CreateCharacters(3);
        EntityId commandId = new("command:career/malformed");
        CareerProposalState valid = Proposal(
            CareerProposalKind.RetinueInvitation,
            Character(0),
            Character(1),
            new ServicePrincipalReference(ServicePrincipalKind.Character, Character(0)),
            null,
            commandId);

        AssertInvalid(CareerWorldSnapshot.Empty with { ContractVersion = 2 }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with { Proposals = null! }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with { Proposals = [valid, valid] }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with
        {
            Proposals = [valid with { ProposalId = new EntityId("career_proposal:wrong") }],
        }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with
        {
            Proposals = [valid with { RecipientCharacterId = Character(0) }],
        }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with
        {
            Proposals = [valid with
            {
                Principal = new ServicePrincipalReference(
                    ServicePrincipalKind.Household,
                    new EntityId("household:career/missing")),
            }],
        }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with
        {
            Proposals = [valid with
            {
                Status = CareerProposalStatus.Refused,
                ResolutionDate = null,
                ResolutionTurnIndex = null,
                ResolutionCommandId = null,
            }],
        }, characters);
        AssertInvalid(CareerWorldSnapshot.Empty with
        {
            Proposals = [Proposal(
                CareerProposalKind.RetinueInvitation,
                Character(0),
                Character(1),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(0)),
                null,
                commandId,
                Date.AddDays(1))],
        }, characters);
    }

    [Fact]
    public void ConstructionRejectsDanglingDuplicateSelfAndInvalidServiceTimelines()
    {
        CharacterWorldState characters = CreateCharacters(4);
        CharacterCareerWorldState state = NewState(characters);
        AcceptRetinue(state, Character(0), Character(1), "malformed-membership");
        CareerWorldSnapshot valid = state.CaptureSnapshot();
        RetinueMembershipState membership = Assert.Single(valid.RetinueMemberships);

        AssertInvalid(valid with { Retinues = [] }, characters);
        AssertInvalid(valid with
        {
            RetinueMemberships = [membership, membership],
        }, characters);
        AssertInvalid(valid with
        {
            RetinueMemberships = [membership with
            {
                MemberCharacterId = membership.LeaderCharacterId,
            }],
        }, characters);
        AssertInvalid(valid with
        {
            RetinueMemberships = [membership with
            {
                EndDate = new CampaignDate(199, 1, 1),
                EndTurnIndex = Calendar.TurnIndex,
                EndCommandId = new EntityId("command:career/invalid-end"),
                EndReason = CareerServiceEndReason.MemberLeft,
            }],
        }, characters);
    }

    [Fact]
    public void RetinueInvitationAcceptLeaveRefuseAndWithdrawalAreEventPlanned()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(4));
        PlannedAction created = Apply(
            state,
            Character(0),
            new RetinueInviteAction(Character(1)),
            "retinue-create");
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(created.Payload.Outcome).Proposal;

        PlannedAction accepted = Apply(
            state,
            Character(1),
            new RespondToRetinueInvitationAction(proposal.ProposalId, CareerProposalResponse.Accept),
            "retinue-accept");
        RetinueInvitationAcceptedOutcome acceptedOutcome =
            Assert.IsType<RetinueInvitationAcceptedOutcome>(accepted.Payload.Outcome);
        Assert.Equal(CareerProposalStatus.Accepted, acceptedOutcome.Proposal.Status);
        Assert.True(Assert.Single(state.RetinueMemberships).IsActive);
        Assert.Equal(Character(0), Assert.Single(state.Retinues).LeaderCharacterId);

        Apply(
            state,
            Character(1),
            new LeaveRetinueAction(acceptedOutcome.Membership.MembershipId),
            "retinue-leave");
        Assert.False(Assert.Single(state.RetinueMemberships).IsActive);
        Assert.Equal(CareerServiceEndReason.MemberLeft, Assert.Single(state.RetinueMemberships).EndReason);

        CareerProposalState refused = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            Character(0),
            new RetinueInviteAction(Character(2)),
            "retinue-refuse-create").Payload.Outcome).Proposal;
        Apply(
            state,
            Character(2),
            new RespondToRetinueInvitationAction(refused.ProposalId, CareerProposalResponse.Refuse),
            "retinue-refuse");
        Assert.Equal(
            CareerProposalStatus.Refused,
            state.Proposals.Single(item => item.ProposalId == refused.ProposalId).Status);

        CareerProposalState withdrawn = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            Character(0),
            new RetinueInviteAction(Character(3)),
            "retinue-withdraw-create").Payload.Outcome).Proposal;
        Apply(
            state,
            Character(0),
            new WithdrawCareerProposalAction(withdrawn.ProposalId),
            "retinue-withdraw");
        Assert.Equal(
            CareerProposalStatus.Withdrawn,
            state.Proposals.Single(item => item.ProposalId == withdrawn.ProposalId).Status);
    }

    [Fact]
    public void PatronageRecommendationAndEmploymentRemainDistinctAndHaveEndFlows()
    {
        CharacterWorldState characters = CreateCharacters(5);
        CharacterCareerWorldState state = NewState(characters);

        CareerProposalState patronage = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            Character(0),
            new PatronageOfferAction(Character(1)),
            "patronage-create").Payload.Outcome).Proposal;
        PatronageOfferAcceptedOutcome patronageAccepted =
            Assert.IsType<PatronageOfferAcceptedOutcome>(Apply(
                state,
                Character(1),
                new RespondToPatronageOfferAction(patronage.ProposalId, CareerProposalResponse.Accept),
                "patronage-accept").Payload.Outcome);
        Apply(
            state,
            Character(0),
            new EndPatronageAction(patronageAccepted.Bond.BondId),
            "patronage-end");
        Assert.Equal(CareerServiceEndReason.PatronEnded, Assert.Single(state.PatronageBonds).EndReason);

        PlannedAction recommendationAction = Apply(
            state,
            Character(2),
            new MakeRecommendationAction(
                Character(3),
                new ServicePrincipalReference(ServicePrincipalKind.Household, Household),
                Role),
            "recommendation");
        RecommendationRecord recommendation = Assert.Single(state.Recommendations);
        Assert.Equal(ServicePrincipalKind.Household, recommendation.Principal.Kind);
        Assert.Contains(
            Role,
            WorldState.GetCharacterActionAffectedIds(
                recommendationAction.Payload,
                recommendationAction.EventId));

        CareerProposalState employment = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            Character(0),
            new EmploymentOfferAction(
                Character(4),
                new ServicePrincipalReference(ServicePrincipalKind.Household, Household),
                Role),
            "employment-create").Payload.Outcome).Proposal;
        EmploymentOfferAcceptedOutcome employed = Assert.IsType<EmploymentOfferAcceptedOutcome>(Apply(
            state,
            Character(4),
            new RespondToEmploymentOfferAction(employment.ProposalId, CareerProposalResponse.Accept),
            "employment-accept").Payload.Outcome);
        Apply(
            state,
            Character(0),
            new EndEmploymentAction(employed.Tenure.TenureId),
            "employment-end");
        EmploymentTenure ended = Assert.Single(state.EmploymentTenures);
        Assert.Equal(CareerServiceEndReason.EmployerEnded, ended.EndReason);
        Assert.NotEqual(ended.Employer.PrincipalId, patronageAccepted.Bond.PatronCharacterId);
        Assert.NotEqual(
            CareerIds.DeriveRetinueId(Character(0)).Value,
            ended.Employer.PrincipalId.Value);
    }

    [Fact]
    public void MultipleRetinueMembershipsAreAllowedWithoutGlobalExclusivity()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(4));
        foreach (int leaderIndex in new[] { 0, 1 })
        {
            CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
                state,
                Character(leaderIndex),
                new RetinueInviteAction(Character(2)),
                $"multi-invite-{leaderIndex}").Payload.Outcome).Proposal;
            Apply(
                state,
                Character(2),
                new RespondToRetinueInvitationAction(proposal.ProposalId, CareerProposalResponse.Accept),
                $"multi-accept-{leaderIndex}");
        }

        Assert.Equal(2, state.RetinueMemberships.Count(item => item.IsActive));
        Assert.Equal(2, state.RetinueMemberships.Select(item => item.RetinueId).Distinct().Count());
    }

    [Fact]
    public void ChangedConditionInvalidatesPendingAcceptanceWithoutCreatingServiceState()
    {
        CharacterWorldState alive = CreateCharacters(3);
        CharacterCareerWorldState initial = NewState(alive);
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            initial,
            Character(0),
            new EmploymentOfferAction(
                Character(1),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(0)),
                Role),
            "invalidate-create").Payload.Outcome).Proposal;

        Dictionary<EntityId, CharacterConditionState> conditions = new()
        {
            [Character(0)] = new(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
        };
        CharacterWorldState changedCharacters = CreateCharacters(3, conditions);
        WorldSnapshot emptyWorld = WorldState.Create(
            Date,
            17,
            [],
            GeographicWorldSnapshot.Empty,
            changedCharacters.CaptureSnapshot(),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty).CaptureSnapshot();
        WorldState world = WorldState.Restore(emptyWorld with
        {
            Calendar = Calendar,
            Careers = initial.CaptureSnapshot(),
        });
        CharacterCareerWorldState changed = world.Careers;
        AssertInvalid(
            changed.ValidateAction(
                Character(1),
                new CharacterActionCommandPayload(new RespondToEmploymentOfferAction(
                    proposal.ProposalId,
                    CareerProposalResponse.Accept)),
                Date,
                Calendar.TurnIndex),
            "proposal_invalidated");

        EntityId commandId = new("command:career/invalidate-resolve");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(Date, commandId);
        RelationshipMemoryConsequenceSpecification staleConsequence = Consequence(
            eventId,
            0,
            Character(1),
            Character(0),
            new RelationshipImpact(10, 0, 0, 0, 0, 0, 0, 0, 0),
            "invalidated_acceptance");
        PlannedAction planned = Plan(
            changed,
            Character(1),
            new RespondToEmploymentOfferAction(proposal.ProposalId, CareerProposalResponse.Accept),
            "invalidate-resolve",
            [staleConsequence]);
        Assert.IsType<CareerProposalInvalidatedOutcome>(planned.Payload.Outcome);
        Assert.Empty(planned.Payload.RelationshipMemoryConsequences);
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            planned.EventId,
            planned.CommandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterActionAffectedIds(planned.Payload, planned.EventId),
            planned.Payload);
        world.Apply(campaignEvent);
        Assert.Empty(changed.EmploymentTenures);
        Assert.Empty(world.Relationships.Subjects);
        Assert.Equal(CareerProposalStatus.Invalidated, Assert.Single(changed.Proposals).Status);
    }

    [Fact]
    public void DeadAndIncapacitatedCannotInitiateWhileCustodyRulesAreActionSpecific()
    {
        Dictionary<EntityId, CharacterConditionState> conditions = new()
        {
            [Character(0)] = new(
                CharacterVitalStatus.Dead,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            [Character(1)] = new(
                CharacterVitalStatus.Alive,
                CharacterHealthStatus.Critical,
                IsIncapacitated: true,
                CharacterCustodyStatus.Free,
                null),
            [Character(2)] = new(
                CharacterVitalStatus.Alive,
                CharacterHealthStatus.Healthy,
                IsIncapacitated: false,
                CharacterCustodyStatus.Detained,
                Character(4)),
        };
        CharacterCareerWorldState state = NewState(CreateCharacters(5, conditions));

        AssertInvalid(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(new PatronageOfferAction(Character(3))),
            Date,
            Calendar.TurnIndex), "actor_dead");
        AssertInvalid(state.ValidateAction(
            Character(1),
            new CharacterActionCommandPayload(new MakeRecommendationAction(
                Character(3),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(4)),
                null)),
            Date,
            Calendar.TurnIndex), "actor_incapacitated");
        AssertInvalid(state.ValidateAction(
            Character(2),
            new CharacterActionCommandPayload(new PatronageOfferAction(Character(3))),
            Date,
            Calendar.TurnIndex), "actor_custody");

        Assert.True(state.ValidateAction(
            Character(3),
            new CharacterActionCommandPayload(new PatronageOfferAction(Character(2))),
            Date,
            Calendar.TurnIndex).IsValid);
        AssertInvalid(state.ValidateAction(
            Character(3),
            new CharacterActionCommandPayload(new RetinueInviteAction(Character(2))),
            Date,
            Calendar.TurnIndex), "recipient_custody");
        AssertInvalid(state.ValidateAction(
            Character(3),
            new CharacterActionCommandPayload(new EmploymentOfferAction(
                Character(2),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(3)),
                Role)),
            Date,
            Calendar.TurnIndex), "recipient_custody");

        CareerProposalState custodyPatronage = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            Character(3),
            new PatronageOfferAction(Character(2)),
            "custody-patronage-create").Payload.Outcome).Proposal;
        Assert.True(state.ValidateAction(
            Character(2),
            new CharacterActionCommandPayload(new RespondToPatronageOfferAction(
                custodyPatronage.ProposalId,
                CareerProposalResponse.Accept)),
            Date,
            Calendar.TurnIndex).IsValid);
        Apply(
            state,
            Character(2),
            new RespondToPatronageOfferAction(
                custodyPatronage.ProposalId,
                CareerProposalResponse.Accept),
            "custody-patronage-accept");
        Assert.True(Assert.Single(state.PatronageBonds).IsActive);
    }

    [Fact]
    public void RelationshipConsequencesAreExplicitDirectionalCanonicalAndDefensive()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(4));
        EntityId commandId = new("command:career/consequence");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(Date, commandId);
        EntityId[] witnesses = [Character(2)];
        RelationshipMemoryConsequenceSpecification consequence = new(
            CareerContractVersions.RelationshipConsequence,
            CareerIds.DeriveRelationshipConsequenceId(eventId, 0),
            Character(1),
            Character(0),
            new RelationshipImpact(0, 1, 2, 0, 3, 0, 0, 0, 0),
            new EntityId("memory_meaning:career/accepted_service"),
            20,
            MemoryPublicity.Witnessed,
            10,
            witnesses);
        CharacterActionResolvedEventPayload planned = state.PlanAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                [consequence]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId,
            [consequence]);

        witnesses[0] = Character(3);
        RelationshipMemoryConsequenceSpecification retained = Assert.Single(
            planned.RelationshipMemoryConsequences);
        Assert.Equal(Character(1), retained.SubjectCharacterId);
        Assert.Equal(Character(0), retained.TargetCharacterId);
        Assert.Equal([Character(2)], retained.WitnessIds);
        Assert.DoesNotContain(
            planned.RelationshipMemoryConsequences,
            item => item.SubjectCharacterId == Character(0)
                && item.TargetCharacterId == Character(1));

        Assert.Throws<SimulationValidationException>(() => state.PlanAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                [consequence with { ConsequenceId = new EntityId("relationship_consequence:wrong") }]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId,
            [consequence with { ConsequenceId = new EntityId("relationship_consequence:wrong") }]));
        Assert.Throws<SimulationValidationException>(() => state.PlanAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                [consequence with { SubjectCharacterId = Character(0), TargetCharacterId = Character(0) }]),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId,
            [consequence with { SubjectCharacterId = Character(0), TargetCharacterId = Character(0) }]));

        Assert.Empty(new CharacterActionCommandPayload(
            new RetinueInviteAction(Character(1))).RelationshipMemoryConsequences);
        AssertInvalid(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                null!),
            Date,
            Calendar.TurnIndex), "invalid_relationship_consequences");
        AssertInvalid(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                [consequence, consequence]),
            Date,
            Calendar.TurnIndex), "noncanonical_relationship_consequences");

        RelationshipMemoryConsequenceSpecification[] tooManyConsequences = Enumerable.Range(
                0,
                CareerLimits.RelationshipConsequencesPerAction + 1)
            .Select(index => Consequence(
                eventId,
                index,
                Character(1),
                Character(0),
                new RelationshipImpact(0, 0, 0, 0, 0, 0, 0, 0, 0),
                $"limit/{index:D2}"))
            .ToArray();
        RelationshipMemoryConsequenceSpecification[] maximumConsequences =
            tooManyConsequences[..CareerLimits.RelationshipConsequencesPerAction];
        Assert.True(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                maximumConsequences),
            Date,
            Calendar.TurnIndex).IsValid);
        Assert.Equal(
            CareerLimits.RelationshipConsequencesPerAction,
            state.PlanAction(
                Character(0),
                new CharacterActionCommandPayload(
                    new RetinueInviteAction(Character(1)),
                    maximumConsequences),
                Date,
                Calendar.TurnIndex,
                commandId,
                eventId,
                maximumConsequences).RelationshipMemoryConsequences.Count);
        AssertInvalid(state.ValidateAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                tooManyConsequences),
            Date,
            Calendar.TurnIndex), "relationship_consequence_limit");
        Assert.Throws<SimulationValidationException>(() => state.PlanAction(
            Character(0),
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                tooManyConsequences),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId,
            tooManyConsequences));
    }

    [Fact]
    public void RegisteredCampaignActionAppliesCareerAndMultipleDirectionalMemories()
    {
        CharacterWorldSnapshot characters = CreateCharacters(4).CaptureSnapshot();
        WorldState world = WorldState.Create(
            Date,
            42,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);
        CampaignSimulation simulation = new(world);
        EntityId commandId = new("command:career/integrated");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(Date, commandId);
        RelationshipMemoryConsequenceSpecification first = Consequence(
            eventId,
            0,
            Character(1),
            Character(0),
            new RelationshipImpact(0, 2, 1, 0, 0, 0, 0, 0, 0),
            "accepted_invitation");
        RelationshipMemoryConsequenceSpecification second = Consequence(
            eventId,
            1,
            Character(2),
            Character(0),
            new RelationshipImpact(1, 0, 0, 0, 2, 0, 0, 0, 0),
            "observed_patronage");
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            Character(0),
            Date,
            new CharacterActionCommandPayload(
                new RetinueInviteAction(Character(1)),
                [first, second]));

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        CharacterActionResolvedEventPayload payload = Assert.IsType<
            CharacterActionResolvedEventPayload>(campaignEvent.Payload);
        Assert.Equal("character_action_resolved.v1", campaignEvent.EventType);
        Assert.Equal(eventId, campaignEvent.EventId);
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(
            payload.Outcome).Proposal;
        Assert.True(world.Careers.TryGetProposal(proposal.ProposalId, out _));
        ConsequentialMemory[] memories = world.Relationships.Subjects
            .SelectMany(subject => subject.DetailedRelationships)
            .SelectMany(relationship => relationship.Memories)
            .OrderBy(memory => memory.ConsequenceIndex)
            .ToArray();
        Assert.Equal(2, memories.Length);
        Assert.All(memories, memory =>
        {
            Assert.Equal(RelationshipContractVersions.Memory, memory.ContractVersion);
            Assert.Equal(RelationshipMemorySourceKind.CharacterAction, memory.SourceKind);
            Assert.Equal(RelationshipMemoryIdentityScheme.SourceEventV2, memory.IdentityScheme);
            Assert.Equal(eventId, memory.SourceEventId);
        });
        Assert.Equal(
            "memory:sha256/aa37cb63979450ffd1cc0349331e5ef07ff09fffd1cb46e0e973cc45a16c645e",
            memories[0].MemoryId.Value);
        Assert.Equal(
            RelationshipIds.DeriveMemoryId(eventId, Character(2), Character(0), 1),
            memories[1].MemoryId);
        Assert.Equal(
            WorldState.GetCharacterActionAffectedIds(payload, eventId),
            campaignEvent.AffectedIds);
        Assert.Contains(world.CaptureSnapshot().SystemVersions, version =>
            version == new SystemVersion(
                "simulation.character_careers",
                CareerContractVersions.Snapshot));
    }

    [Fact]
    public void CrossSubsystemPreparationRejectsOverflowWithoutPartialCareerMutation()
    {
        CharacterWorldSnapshot characters = CreateCharacters(3).CaptureSnapshot();
        WorldState world = WorldState.Create(
            Date,
            9,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);
        EntityId commandId = new("command:career/atomic-overflow");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(Date, commandId);
        RelationshipImpact impact = new(60, 0, 0, 0, 0, 0, 0, 0, 0);
        RelationshipMemoryConsequenceSpecification first = Consequence(
            eventId,
            0,
            Character(1),
            Character(0),
            impact,
            "atomic_first");
        RelationshipMemoryConsequenceSpecification second = Consequence(
            eventId,
            1,
            Character(1),
            Character(0),
            impact,
            "atomic_second");
        CharacterActionCommandPayload command = new(
            new RetinueInviteAction(Character(1)),
            [first, second]);
        CharacterActionResolvedEventPayload planned = world.Careers.PlanAction(
            Character(0),
            command,
            Date,
            world.Calendar.TurnIndex,
            commandId,
            eventId,
            command.RelationshipMemoryConsequences);
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterActionAffectedIds(planned, eventId),
            planned);
        string careerBefore = Serialize(world.Careers.CaptureSnapshot());
        string relationshipBefore = Serialize(world.Relationships.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => world.Apply(campaignEvent));

        Assert.Equal(careerBefore, Serialize(world.Careers.CaptureSnapshot()));
        Assert.Equal(relationshipBefore, Serialize(world.Relationships.CaptureSnapshot()));
    }

    [Fact]
    public void CurrentSaveRoundTripsPendingDiagnosticsAndRegisteredCharacterPayloads()
    {
        CharacterWorldSnapshot characters = CreateCharacters(3).CaptureSnapshot();
        WorldState world = WorldState.Create(
            Date,
            77,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);
        CampaignSimulation simulation = new(world);
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:career/save-roundtrip"),
            Character(0),
            Date,
            new CharacterActionCommandPayload(new PatronageOfferAction(Character(1))));
        Assert.True(simulation.Submit(command).IsValid);
        Assert.IsType<CharacterActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        SaveEnvelope expected = SaveEnvelope.Create(
            "0.1.0",
            [],
            simulation,
            DateTimeOffset.Parse(
                "2026-07-15T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));
        string path = Path.Combine(
            Path.GetTempPath(),
            $"career-save-{Guid.NewGuid():N}.save.gz");
        try
        {
            new SaveStore().SaveAtomic(path, expected);
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsType<CharacterActionCommandPayload>(
                Assert.Single(loaded.DiagnosticCommands).Payload);
            Assert.IsType<CharacterActionResolvedEventPayload>(
                Assert.Single(loaded.DiagnosticEvents).Payload);
            Assert.Single(loaded.Snapshot.Careers.Proposals);
            Assert.Equal(expected.Checksum, loaded.Checksum);
            _ = WorldState.Restore(loaded.Snapshot);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ThousandCharacterCareerRelationshipAndSaveFixtureRemainsBounded()
    {
        const int categorySize = 150;
        CharacterWorldSnapshot characters = CreateCharacters(1_000).CaptureSnapshot();
        WorldState world = WorldState.Create(
            Date,
            20260715,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty);
        CampaignSimulation simulation = new(world);
        Stopwatch turnProcessing = Stopwatch.StartNew();

        for (int index = 0; index < categorySize; index++)
        {
            SubmitPerformanceAction(
                simulation,
                $"retinue-offer-{index:D3}",
                Character(index),
                new RetinueInviteAction(Character(500 + index)),
                Character(500 + index),
                Character(index));
            SubmitPerformanceAction(
                simulation,
                $"patronage-offer-{index:D3}",
                Character(150 + index),
                new PatronageOfferAction(Character(650 + index)),
                Character(650 + index),
                Character(150 + index));
            SubmitPerformanceAction(
                simulation,
                $"employment-offer-{index:D3}",
                Character(300 + index),
                new EmploymentOfferAction(
                    Character(800 + index),
                    new ServicePrincipalReference(
                        ServicePrincipalKind.Character,
                        Character(300 + index)),
                    Role),
                Character(800 + index),
                Character(300 + index));
        }

        Assert.Equal(450, simulation.ResolveTurn().Count);
        Dictionary<(CareerProposalKind Kind, EntityId Recipient), CareerProposalState> proposals =
            world.Careers.Proposals.ToDictionary(
                proposal => (proposal.Kind, proposal.RecipientCharacterId));
        for (int index = 0; index < categorySize; index++)
        {
            EntityId retinueRecipient = Character(500 + index);
            SubmitPerformanceAction(
                simulation,
                $"retinue-accept-{index:D3}",
                retinueRecipient,
                new RespondToRetinueInvitationAction(
                    proposals[(CareerProposalKind.RetinueInvitation, retinueRecipient)].ProposalId,
                    CareerProposalResponse.Accept),
                Character(index),
                retinueRecipient);
            EntityId patronageRecipient = Character(650 + index);
            SubmitPerformanceAction(
                simulation,
                $"patronage-accept-{index:D3}",
                patronageRecipient,
                new RespondToPatronageOfferAction(
                    proposals[(CareerProposalKind.PatronageOffer, patronageRecipient)].ProposalId,
                    CareerProposalResponse.Accept),
                Character(150 + index),
                patronageRecipient);
            EntityId employee = Character(800 + index);
            SubmitPerformanceAction(
                simulation,
                $"employment-accept-{index:D3}",
                employee,
                new RespondToEmploymentOfferAction(
                    proposals[(CareerProposalKind.EmploymentOffer, employee)].ProposalId,
                    CareerProposalResponse.Accept),
                Character(300 + index),
                employee);
        }

        for (int index = 0; index < 100; index++)
        {
            EntityId recommender = Character(450 + (index % 50));
            EntityId beneficiary = Character(950 + (index % 50));
            SubmitPerformanceAction(
                simulation,
                $"recommendation-{index:D3}",
                recommender,
                new MakeRecommendationAction(
                    beneficiary,
                    new ServicePrincipalReference(
                        ServicePrincipalKind.Character,
                        Character(100 + index)),
                    Role),
                beneficiary,
                recommender);
        }

        Assert.Equal(550, simulation.ResolveTurn().Count);
        turnProcessing.Stop();

        Stopwatch careerQuery = Stopwatch.StartNew();
        Assert.Equal(categorySize, world.Careers.RetinueMemberships.Count(item => item.IsActive));
        Assert.Equal(categorySize, world.Careers.PatronageBonds.Count(item => item.IsActive));
        Assert.Equal(categorySize, world.Careers.EmploymentTenures.Count(item => item.IsActive));
        Assert.Equal(100, world.Careers.Recommendations.Count);
        Assert.Equal(450, world.Careers.Proposals.Count);
        Assert.All(world.Careers.Proposals, proposal =>
            Assert.Equal(CareerProposalStatus.Accepted, proposal.Status));
        careerQuery.Stop();

        Stopwatch snapshotAndChecksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = world.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        snapshotAndChecksum.Stop();
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.Equal(1_000, snapshot.Relationships.Subjects.Sum(subject =>
            subject.DetailedRelationships.Sum(relationship => relationship.Memories.Count)));
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));

        SaveEnvelope envelope = SaveEnvelope.Create(
            "0.1.0",
            [],
            simulation,
            DateTimeOffset.Parse(
                "2026-07-15T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));
        string path = Path.Combine(
            Path.GetTempPath(),
            $"career-performance-{Guid.NewGuid():N}.save.gz");
        try
        {
            Stopwatch save = Stopwatch.StartNew();
            new SaveStore().SaveAtomic(path, envelope);
            save.Stop();
            Stopwatch load = Stopwatch.StartNew();
            SaveEnvelope loaded = new SaveStore().Load(path);
            load.Stop();

            Assert.Equal(envelope.Checksum, loaded.Checksum);
            Assert.Equal(450, loaded.Snapshot.Careers.Proposals.Count);
            _ = WorldState.Restore(loaded.Snapshot);
            output.WriteLine(
                $"SP-04C1 raw Apple Silicon fixture: turn_processing_ms={turnProcessing.Elapsed.TotalMilliseconds:F3}; "
                + $"career_query_ms={careerQuery.Elapsed.TotalMilliseconds:F3}; "
                + $"snapshot_checksum_ms={snapshotAndChecksum.Elapsed.TotalMilliseconds:F3}; "
                + $"save_ms={save.Elapsed.TotalMilliseconds:F3}; load_ms={load.Elapsed.TotalMilliseconds:F3}; "
                + $"save_bytes={new FileInfo(path).Length}; checksum={checksum.Value}");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void PlanningAndPrevalidationDoNotMutateAndTamperedOutcomeAppliesNothing()
    {
        Assert.Null(typeof(CharacterCareerWorldState).GetMethod(
            "ApplyOutcome",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public));
        CharacterCareerWorldState state = NewState(CreateCharacters(3));
        string before = Serialize(state.CaptureSnapshot());
        PlannedAction planned = Plan(
            state,
            Character(0),
            new RetinueInviteAction(Character(1)),
            "atomic-plan");

        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
        state.PrevalidateOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));

        CareerProposalCreatedOutcome original =
            Assert.IsType<CareerProposalCreatedOutcome>(planned.Payload.Outcome);
        CharacterActionResolvedEventPayload tampered = planned.Payload with
        {
            Outcome = new CareerProposalCreatedOutcome(original.Proposal with
            {
                RecipientCharacterId = Character(2),
            }),
        };
        Assert.Throws<SimulationValidationException>(() => state.ApplyOutcome(
            tampered,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));

        state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);
        Assert.Single(state.Proposals);
    }

    [Fact]
    public void ActiveProposalLimitIsExactlyEightPerRecipient()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(11));
        for (int index = 0; index < CareerLimits.ActiveProposalsPerRecipient; index++)
        {
            Apply(
                state,
                Character(index),
                new PatronageOfferAction(Character(10)),
                $"proposal-limit-{index}");
        }

        Assert.Equal(8, state.Proposals.Count(item => item.Status == CareerProposalStatus.Active));
        AssertInvalid(state.ValidateAction(
            Character(8),
            new CharacterActionCommandPayload(new PatronageOfferAction(Character(10))),
            Date,
            Calendar.TurnIndex), "proposal_recipient_limit");
    }

    [Fact]
    public void ActiveServiceBoundsAreExactAndCapacityAcceptanceInvalidates()
    {
        CharacterWorldState characters = CreateCharacters(70);
        CharacterCareerWorldState memberships = NewState(characters);
        for (int index = 1; index <= CareerLimits.ActiveMembershipsPerRetinue; index++)
        {
            AcceptRetinue(memberships, Character(0), Character(index), $"membership-limit-{index}");
        }

        CareerProposalState overflowMembership = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            memberships,
            Character(0),
            new RetinueInviteAction(Character(65)),
            "membership-overflow-create").Payload.Outcome).Proposal;
        PlannedAction membershipResponse = Plan(
            memberships,
            Character(65),
            new RespondToRetinueInvitationAction(
                overflowMembership.ProposalId,
                CareerProposalResponse.Accept),
            "membership-overflow-accept");
        Assert.IsType<CareerProposalInvalidatedOutcome>(membershipResponse.Payload.Outcome);

        CharacterCareerWorldState patronage = NewState(characters);
        for (int index = 1; index <= CareerLimits.ActivePatronageBondsPerCharacter; index++)
        {
            AcceptPatronage(patronage, Character(index), Character(0), $"patronage-limit-{index}");
        }

        CareerProposalState overflowPatronage = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            patronage,
            Character(17),
            new PatronageOfferAction(Character(0)),
            "patronage-overflow-create").Payload.Outcome).Proposal;
        Assert.IsType<CareerProposalInvalidatedOutcome>(Plan(
            patronage,
            Character(0),
            new RespondToPatronageOfferAction(
                overflowPatronage.ProposalId,
                CareerProposalResponse.Accept),
            "patronage-overflow-accept").Payload.Outcome);

        CharacterCareerWorldState employment = NewState(characters);
        for (int index = 1; index <= CareerLimits.ActiveEmploymentTenuresPerEmployee; index++)
        {
            AcceptEmployment(
                employment,
                Character(index),
                Character(0),
                new EntityId($"role:career/r{index:D2}"),
                $"employment-limit-{index}");
        }

        CareerProposalState overflowEmployment = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            employment,
            Character(9),
            new EmploymentOfferAction(
                Character(0),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(9)),
                new EntityId("role:career/overflow")),
            "employment-overflow-create").Payload.Outcome).Proposal;
        Assert.IsType<CareerProposalInvalidatedOutcome>(Plan(
            employment,
            Character(0),
            new RespondToEmploymentOfferAction(
                overflowEmployment.ProposalId,
                CareerProposalResponse.Accept),
            "employment-overflow-accept").Payload.Outcome);
    }

    [Fact]
    public void CompletedRecommendationsAndProposalsEvictIntoFixedPerCharacterHistory()
    {
        CharacterCareerWorldState recommendations = NewState(CreateCharacters(4));
        for (int index = 0; index < 66; index++)
        {
            Apply(
                recommendations,
                Character(0),
                new MakeRecommendationAction(
                    Character(1),
                    new ServicePrincipalReference(ServicePrincipalKind.Character, Character(2)),
                    Role),
                $"recommend-retain-{index:D3}");
        }

        Assert.Equal(CareerLimits.RecommendationsPerCharacter, recommendations.Recommendations.Count);
        Assert.Equal(2, GetHistory(recommendations, Character(0)).FoldedRecommendationCount);
        Assert.Equal(2, GetHistory(recommendations, Character(1)).FoldedRecommendationCount);
        Assert.Equal(2, GetHistory(recommendations, Character(2)).FoldedRecommendationCount);

        CharacterCareerWorldState proposals = NewState(CreateCharacters(3));
        for (int index = 0; index < 65; index++)
        {
            CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
                proposals,
                Character(0),
                new RetinueInviteAction(Character(1)),
                $"proposal-retain-create-{index:D3}").Payload.Outcome).Proposal;
            Apply(
                proposals,
                Character(1),
                new RespondToRetinueInvitationAction(
                    proposal.ProposalId,
                    CareerProposalResponse.Refuse),
                $"proposal-retain-refuse-{index:D3}");
        }

        Assert.Equal(CareerLimits.CompletedRecordsPerCategoryPerCharacter, proposals.Proposals.Count);
        Assert.Equal(1, GetHistory(proposals, Character(0)).FoldedRetinueProposalCount);
        Assert.Equal(1, GetHistory(proposals, Character(1)).FoldedRetinueProposalCount);
    }

    [Fact]
    public void EveryCompletedServiceCategoryRetainsSixtyFourAndFoldsEvictedDetail()
    {
        CharacterWorldState characters = CreateCharacters(4);
        CharacterCareerWorldState memberships = NewState(characters);
        CharacterCareerWorldState patronage = NewState(characters);
        CharacterCareerWorldState employment = NewState(characters);
        for (int index = 0; index < 65; index++)
        {
            string suffix = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

            CareerProposalState membershipProposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
                memberships,
                Character(0),
                new RetinueInviteAction(Character(1)),
                $"fold-membership-create-{suffix}").Payload.Outcome).Proposal;
            RetinueInvitationAcceptedOutcome membership =
                Assert.IsType<RetinueInvitationAcceptedOutcome>(Apply(
                    memberships,
                    Character(1),
                    new RespondToRetinueInvitationAction(
                        membershipProposal.ProposalId,
                        CareerProposalResponse.Accept),
                    $"fold-membership-accept-{suffix}").Payload.Outcome);
            Apply(
                memberships,
                Character(1),
                new LeaveRetinueAction(membership.Membership.MembershipId),
                $"fold-membership-end-{suffix}");

            CareerProposalState patronageProposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
                patronage,
                Character(0),
                new PatronageOfferAction(Character(1)),
                $"fold-patronage-create-{suffix}").Payload.Outcome).Proposal;
            PatronageOfferAcceptedOutcome bond = Assert.IsType<PatronageOfferAcceptedOutcome>(Apply(
                patronage,
                Character(1),
                new RespondToPatronageOfferAction(
                    patronageProposal.ProposalId,
                    CareerProposalResponse.Accept),
                $"fold-patronage-accept-{suffix}").Payload.Outcome);
            Apply(
                patronage,
                Character(0),
                new EndPatronageAction(bond.Bond.BondId),
                $"fold-patronage-end-{suffix}");

            EntityId role = new($"role:career/fold_{suffix}");
            CareerProposalState employmentProposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
                employment,
                Character(0),
                new EmploymentOfferAction(
                    Character(1),
                    new ServicePrincipalReference(ServicePrincipalKind.Character, Character(0)),
                    role),
                $"fold-employment-create-{suffix}").Payload.Outcome).Proposal;
            EmploymentOfferAcceptedOutcome tenure = Assert.IsType<EmploymentOfferAcceptedOutcome>(Apply(
                employment,
                Character(1),
                new RespondToEmploymentOfferAction(
                    employmentProposal.ProposalId,
                    CareerProposalResponse.Accept),
                $"fold-employment-accept-{suffix}").Payload.Outcome);
            Apply(
                employment,
                Character(1),
                new EndEmploymentAction(tenure.Tenure.TenureId),
                $"fold-employment-end-{suffix}");
        }

        Assert.Equal(64, memberships.RetinueMemberships.Count);
        Assert.Equal(1, GetHistory(memberships, Character(0)).FoldedRetinueMembershipCount);
        Assert.Equal(64, patronage.PatronageBonds.Count);
        Assert.Equal(1, GetHistory(patronage, Character(0)).FoldedPatronageBondCount);
        Assert.Equal(64, employment.EmploymentTenures.Count);
        Assert.Equal(1, GetHistory(employment, Character(0)).FoldedEmploymentTenureCount);
    }

    [Fact]
    public void CheckedHistoryOverflowRejectsWholeApplication()
    {
        CharacterWorldState characters = CreateCharacters(4);
        RecommendationRecord[] retained = Enumerable.Range(0, CareerLimits.RecommendationsPerCharacter)
            .Select(index => Recommendation(
                Character(0),
                Character(1),
                Character(2),
                $"overflow-seed-{index:D3}"))
            .ToArray();
        CareerHistoryAggregate aggregate = CareerHistoryAggregate.Empty(Character(0)) with
        {
            FoldedRecommendationCount = long.MaxValue,
            EarliestDate = Date,
            LatestDate = Date,
        };
        CharacterCareerWorldState state = new(
            CareerWorldSnapshot.Empty with
            {
                Recommendations = retained,
                History = [aggregate],
            },
            characters,
            Calendar);
        string before = Serialize(state.CaptureSnapshot());
        PlannedAction planned = Plan(
            state,
            Character(0),
            new MakeRecommendationAction(
                Character(1),
                new ServicePrincipalReference(ServicePrincipalKind.Character, Character(2)),
                Role),
            "overflow-trigger");

        Assert.Throws<SimulationValidationException>(() => state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void ClosedActionAndOutcomeUnionsRoundTripWithExplicitDiscriminators()
    {
        CharacterCareerWorldState state = NewState(CreateCharacters(3));
        PlannedAction planned = Plan(
            state,
            Character(0),
            new RetinueInviteAction(Character(1)),
            "json-union");
        JsonSerializerOptions options = SimulationJson.CreateOptions();

        string commandJson = JsonSerializer.Serialize(
            new CharacterActionCommandPayload(new RetinueInviteAction(Character(1))),
            options);
        CharacterActionCommandPayload command = JsonSerializer.Deserialize<CharacterActionCommandPayload>(
            commandJson,
            options)!;
        Assert.IsType<RetinueInviteAction>(command.Action);
        Assert.Empty(command.RelationshipMemoryConsequences);

        string eventJson = JsonSerializer.Serialize(planned.Payload, options);
        CharacterActionResolvedEventPayload restored =
            JsonSerializer.Deserialize<CharacterActionResolvedEventPayload>(eventJson, options)!;
        Assert.IsType<RetinueInviteAction>(restored.Action);
        Assert.IsType<CareerProposalCreatedOutcome>(restored.Outcome);
        Assert.Contains("retinue_invite.v1", eventJson, StringComparison.Ordinal);
        Assert.Contains("career_proposal_created.v1", eventJson, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CharacterActionCommandPayload>(
            commandJson.Replace("retinue_invite.v1", "retinue_invite.v999", StringComparison.Ordinal),
            options));

        CampaignCommand outerCommand = CampaignCommand.Create(
            planned.CommandId,
            Character(0),
            Date,
            new CharacterActionCommandPayload(new RetinueInviteAction(Character(1))));
        string outerCommandJson = JsonSerializer.Serialize(outerCommand, options);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CampaignCommand>(
            outerCommandJson.Replace(
                "character_action.v1",
                "character_action.v999",
                StringComparison.Ordinal),
            options));
        CampaignEvent outerEvent = new(
            ContractVersions.CampaignEvent,
            planned.EventId,
            planned.CommandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterActionAffectedIds(planned.Payload, planned.EventId),
            planned.Payload);
        string outerEventJson = JsonSerializer.Serialize(outerEvent, options);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CampaignEvent>(
            outerEventJson.Replace(
                "character_action_resolved.v1",
                "character_action_resolved.v999",
                StringComparison.Ordinal),
            options));
    }

    private static CharacterCareerWorldState NewState(CharacterWorldState characters) =>
        new(CareerWorldSnapshot.Empty, characters, Calendar);

    private static PlannedAction Plan(
        CharacterCareerWorldState state,
        EntityId actor,
        ICharacterAction action,
        string commandSuffix,
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? consequences = null)
    {
        EntityId commandId = new($"command:career/{commandSuffix}");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(Date, commandId);
        CharacterActionResolvedEventPayload payload = state.PlanAction(
            actor,
            new CharacterActionCommandPayload(action, consequences ?? []),
            Date,
            Calendar.TurnIndex,
            commandId,
            eventId,
            consequences ?? []);
        return new PlannedAction(payload, commandId, eventId);
    }

    private static PlannedAction Apply(
        CharacterCareerWorldState state,
        EntityId actor,
        ICharacterAction action,
        string commandSuffix,
        IReadOnlyList<RelationshipMemoryConsequenceSpecification>? consequences = null)
    {
        PlannedAction planned = Plan(state, actor, action, commandSuffix, consequences);
        state.ApplyOutcome(
            planned.Payload,
            Date,
            Calendar.TurnIndex,
            planned.CommandId,
            planned.EventId);
        return planned;
    }

    private static void AcceptRetinue(
        CharacterCareerWorldState state,
        EntityId leader,
        EntityId member,
        string suffix)
    {
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            leader,
            new RetinueInviteAction(member),
            $"{suffix}-create").Payload.Outcome).Proposal;
        Apply(
            state,
            member,
            new RespondToRetinueInvitationAction(proposal.ProposalId, CareerProposalResponse.Accept),
            $"{suffix}-accept");
    }

    private static void AcceptPatronage(
        CharacterCareerWorldState state,
        EntityId patron,
        EntityId beneficiary,
        string suffix)
    {
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            patron,
            new PatronageOfferAction(beneficiary),
            $"{suffix}-create").Payload.Outcome).Proposal;
        Apply(
            state,
            beneficiary,
            new RespondToPatronageOfferAction(proposal.ProposalId, CareerProposalResponse.Accept),
            $"{suffix}-accept");
    }

    private static void AcceptEmployment(
        CharacterCareerWorldState state,
        EntityId employer,
        EntityId employee,
        EntityId role,
        string suffix)
    {
        CareerProposalState proposal = Assert.IsType<CareerProposalCreatedOutcome>(Apply(
            state,
            employer,
            new EmploymentOfferAction(
                employee,
                new ServicePrincipalReference(ServicePrincipalKind.Character, employer),
                role),
            $"{suffix}-create").Payload.Outcome).Proposal;
        Apply(
            state,
            employee,
            new RespondToEmploymentOfferAction(proposal.ProposalId, CareerProposalResponse.Accept),
            $"{suffix}-accept");
    }

    private static CareerHistoryAggregate GetHistory(
        CharacterCareerWorldState state,
        EntityId characterId)
    {
        Assert.True(state.TryGetHistory(characterId, out CareerHistoryAggregate? aggregate));
        return aggregate;
    }

    private static CareerProposalState Proposal(
        CareerProposalKind kind,
        EntityId proposer,
        EntityId recipient,
        ServicePrincipalReference principal,
        EntityId? role,
        EntityId commandId,
        CampaignDate? createdDate = null)
    {
        CampaignDate date = createdDate ?? Date;
        return new CareerProposalState(
            CareerContractVersions.State,
            CareerIds.DeriveProposalId(kind, date, commandId),
            kind,
            proposer,
            recipient,
            principal,
            role,
            date,
            Calendar.TurnIndex,
            commandId,
            CareerProposalStatus.Active,
            null,
            null,
            null);
    }

    private static RecommendationRecord Recommendation(
        EntityId recommender,
        EntityId beneficiary,
        EntityId principal,
        string suffix)
    {
        EntityId commandId = new($"command:career/{suffix}");
        return new RecommendationRecord(
            CareerContractVersions.State,
            CareerIds.DeriveRecommendationId(Date, commandId),
            recommender,
            beneficiary,
            new ServicePrincipalReference(ServicePrincipalKind.Character, principal),
            Role,
            Date,
            Calendar.TurnIndex,
            commandId);
    }

    private static CharacterWorldState CreateCharacters(
        int count,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, count)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:career/character_{index:D3}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    new CampaignDate(160, 1, 1),
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = definitions
            .Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                [],
                [],
                conditions is not null && conditions.TryGetValue(definition.Id, out CharacterConditionState? value)
                    ? value
                    : CharacterConditionState.Default))
            .ToArray();
        HouseholdDefinition[] householdDefinitions = count == 0
            ? []
            : [new HouseholdDefinition(
                CharacterContractVersions.Definition,
                Household,
                new EntityId("loc:career/household"))];
        HouseholdState[] householdStates = count == 0
            ? []
            : [new HouseholdState(
                CharacterContractVersions.State,
                Household,
                Character(0),
                definitions.Select(item => item.Id).Order().ToArray())];
        CharacterWorldSnapshot snapshot = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            householdDefinitions,
            states,
            [],
            householdStates);
        return new CharacterWorldState(snapshot, Date);
    }

    private static RelationshipMemoryConsequenceSpecification Consequence(
        EntityId eventId,
        int index,
        EntityId subject,
        EntityId target,
        RelationshipImpact impact,
        string meaningSuffix) => new(
            CareerContractVersions.RelationshipConsequence,
            CareerIds.DeriveRelationshipConsequenceId(eventId, index),
            subject,
            target,
            impact,
            new EntityId($"memory_meaning:career/{meaningSuffix}"),
            20,
            MemoryPublicity.Private,
            0,
            []);

    private static void SubmitPerformanceAction(
        CampaignSimulation simulation,
        string suffix,
        EntityId actor,
        ICharacterAction action,
        EntityId memorySubject,
        EntityId memoryTarget)
    {
        EntityId commandId = new($"command:career/performance/{suffix}");
        EntityId eventId = CareerIds.DeriveCharacterActionEventId(
            simulation.World.Calendar.Date,
            commandId);
        RelationshipMemoryConsequenceSpecification consequence = Consequence(
            eventId,
            0,
            memorySubject,
            memoryTarget,
            new RelationshipImpact(0, 1, 0, 0, 0, 0, 0, 0, 0),
            $"performance/{suffix}");
        CampaignCommand command = CampaignCommand.Create(
            commandId,
            actor,
            simulation.World.Calendar.Date,
            new CharacterActionCommandPayload(action, [consequence]));
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Issues.Select(issue => issue.Message)));
    }

    private static EntityId Character(int index) =>
        new($"character:career/c{index:D3}");

    private static void AssertInvalid(
        CareerWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters) =>
        Assert.Throws<SimulationValidationException>(() =>
            new CharacterCareerWorldState(snapshot, characters, Calendar));

    private static void AssertInvalid(CommandValidationResult result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record PlannedAction(
        CharacterActionResolvedEventPayload Payload,
        EntityId CommandId,
        EntityId EventId);
}
