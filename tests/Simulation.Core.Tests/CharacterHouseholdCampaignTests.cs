using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterHouseholdCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId PracticeId = new("marriage_practice:test/d3");
    private readonly ITestOutputHelper output;

    public CharacterHouseholdCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void D301_NewCommandActionOutcomeAndEventDiscriminatorsRoundTrip()
    {
        CharacterConditionState expected = CharacterConditionState.Default;
        ICharacterConditionAction[] conditionActions =
        [
            new IncapacitateCharacterAction(Character(1), expected),
            new RestoreCharacterCapacityAction(Character(1), expected),
            new EnterCharacterCustodyAction(
                Character(1),
                expected,
                CharacterCustodyStatus.Captive,
                Character(0)),
            new ReleaseCharacterCustodyAction(Character(1), expected),
        ];
        string[] conditionDiscriminators =
        [
            "incapacitate_character.v1",
            "restore_character_capacity.v1",
            "enter_character_custody.v1",
            "release_character_custody.v1",
        ];
        for (int index = 0; index < conditionActions.Length; index++)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:d3/condition-discriminator-{index}"),
                CharacterConditionSystem.AuthoritativeActorId,
                Date,
                new CharacterConditionActionCommandPayload(conditionActions[index]));
            string json = JsonSerializer.Serialize(command, SimulationJson.CreateOptions());
            CampaignCommand roundTrip = JsonSerializer.Deserialize<CampaignCommand>(
                json,
                SimulationJson.CreateOptions())!;

            Assert.Equal("character_condition_action.v1", command.CommandType);
            Assert.Contains("character_condition_action.v1", json, StringComparison.Ordinal);
            Assert.Contains(conditionDiscriminators[index], json, StringComparison.Ordinal);
            Assert.Equal(
                conditionActions[index].GetType(),
                Assert.IsType<CharacterConditionActionCommandPayload>(
                    roundTrip.Payload).Action.GetType());
        }

        IHouseholdDecisionAction[] householdActions =
        [
            new ExpelHouseholdMemberAction(Household(0), Character(1)),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
        ];
        foreach (IHouseholdDecisionAction action in householdActions)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:d3/household-{action.GetType().Name.ToLowerInvariant()}"),
                Character(0),
                Date,
                new HouseholdDecisionCommandPayload(action));
            string json = JsonSerializer.Serialize(command, SimulationJson.CreateOptions());
            CampaignCommand roundTrip = JsonSerializer.Deserialize<CampaignCommand>(
                json,
                SimulationJson.CreateOptions())!;

            Assert.Equal("household_decision.v1", command.CommandType);
            Assert.Contains("household_decision.v1", json, StringComparison.Ordinal);
            Assert.Equal(
                action.GetType(),
                Assert.IsType<HouseholdDecisionCommandPayload>(roundTrip.Payload).Action.GetType());
        }

        CampaignCommand coercive = CampaignCommand.Create(
            new EntityId("command:d3/coercive-discriminator"),
            Character(0),
            Date,
            new CharacterMarriageActionCommandPayload(new ImposeCoercedUnionAction(
                Character(1),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId)));
        string coerciveJson = JsonSerializer.Serialize(coercive, SimulationJson.CreateOptions());
        Assert.Contains("impose_coerced_union.v1", coerciveJson, StringComparison.Ordinal);
        Assert.IsType<ImposeCoercedUnionAction>(
            Assert.IsType<CharacterMarriageActionCommandPayload>(
                JsonSerializer.Deserialize<CampaignCommand>(
                    coerciveJson,
                    SimulationJson.CreateOptions())!.Payload).Action);

        EntityId goldenCommand = new("command:d3/golden");
        EntityId conditionEvent = CharacterConditionIds.DeriveActionEventId(
            Date,
            goldenCommand);
        EntityId householdEvent = HouseholdDecisionIds.DeriveActionEventId(
            Date,
            goldenCommand);
        Assert.Equal(
            "event:sha256/dc528d140d9102ae234956cdd537811284910331f4c8abe422bf784accd26f08",
            conditionEvent.Value);
        Assert.Equal(
            "character_condition_change:sha256/2f0d61891e442c3c3c0695cab95921b156b8c6d1c2b21e8442a5c57009d4662e",
            CharacterConditionIds.DeriveChangeId(conditionEvent, Character(1)).Value);
        Assert.Equal(
            "event:sha256/c96a9681df921fb58be7730a6f18e9447e54be4960d08e8dd89ede58e7a968c6",
            householdEvent.Value);
        Assert.Equal(
            "household_transition:sha256/0fc62d57581db925231a64aebf447d4246423031c54d97ec136cc6519c260e43",
            HouseholdDecisionIds.DeriveTransitionId(
                householdEvent,
                Character(1)).Value);
    }

    [Fact]
    public void D302_ConditionAuthorityExpectedCurrentAndLifecycleAreAtomic()
    {
        CampaignSimulation simulation = CreateSimulation(6);
        CharacterWorldState characterReference = simulation.World.Characters;

        MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new ProposePoliticalMarriageAction(
                    Character(2),
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "lifecycle-proposal").Payload).Outcome).Proposal;
        RomanceInvitationState invitation = Assert.IsType<RomanceInvitationCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new OfferRomanceRouteAction(Character(3), PracticeId),
                "lifecycle-invitation").Payload).Outcome).Invitation;
        RomanceInvitationState routeInvitation = Assert.IsType<RomanceInvitationCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new OfferRomanceRouteAction(Character(4), PracticeId),
                "lifecycle-route-offer").Payload).Outcome).Invitation;
        RomanceRouteState route = Assert.IsType<RomanceRouteStartedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(4),
                new RespondToRomanceInvitationAction(
                    routeInvitation.InvitationId,
                    RomanceInvitationResponse.Accept),
                "lifecycle-route-accept").Payload).Outcome).Route;
        MarriageProposalState betrothalProposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new ProposePoliticalMarriageAction(
                    Character(5),
                    MarriageProposalKind.PoliticalBetrothal,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "lifecycle-betrothal-offer").Payload).Outcome).Proposal;
        PoliticalBetrothalState betrothal = Assert.IsType<PoliticalBetrothalAcceptedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(5),
                new RespondToPoliticalMarriageProposalAction(
                    betrothalProposal.ProposalId,
                    MarriageProposalResponse.Accept),
                "lifecycle-betrothal-accept").Payload).Outcome).Betrothal;

        CharacterConditionState current = Condition(simulation, Character(1));
        CampaignCommand stale = ConditionCommand(
            simulation,
            new IncapacitateCharacterAction(
                Character(1),
                current with { HealthStatus = CharacterHealthStatus.Ill }),
            "stale");
        Assert.False(simulation.Submit(stale).IsValid);
        CampaignCommand unauthorized = CampaignCommand.Create(
            new EntityId("command:d3/condition-unauthorized"),
            Character(0),
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new IncapacitateCharacterAction(Character(1), current)));
        Assert.False(simulation.Submit(unauthorized).IsValid);

        CampaignEvent campaignEvent = SubmitCondition(
            simulation,
            new IncapacitateCharacterAction(Character(1), current),
            "incapacitate-lifecycle");
        CharacterConditionActionResolvedEventPayload resolved = Assert.IsType<
            CharacterConditionActionResolvedEventPayload>(campaignEvent.Payload);
        CharacterConditionChangedOutcome outcome = Assert.IsType<
            CharacterConditionChangedOutcome>(resolved.Outcome);

        Assert.Same(characterReference, simulation.World.Characters);
        Assert.True(Condition(simulation, Character(1)).IsIncapacitated);
        Assert.Equal(CharacterMarriageLifecycleReason.ConditionChanged, outcome.MarriageChanges.Reason);
        Assert.Equal(proposal.ProposalId, Assert.Single(
            outcome.MarriageChanges.InvalidatedProposals).ProposalId);
        Assert.Equal(invitation.InvitationId, Assert.Single(
            outcome.MarriageChanges.CancelledInvitations).InvitationId);
        Assert.Equal(route.RouteId, Assert.Single(
            outcome.MarriageChanges.InvalidatedRomanceRoutes).RouteId);
        Assert.Empty(outcome.MarriageChanges.InvalidatedBetrothals);
        Assert.Equal(
            PoliticalBetrothalStatus.Active,
            simulation.World.CharacterMarriages.Betrothals.Single(
                value => value.BetrothalId == betrothal.BetrothalId).Status);
        Assert.Equal(
            MarriageProposalStatus.Invalidated,
            simulation.World.CharacterMarriages.Proposals.Single(
                value => value.ProposalId == proposal.ProposalId).Status);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Equal(
            RomanceRouteStatus.Invalidated,
            simulation.World.CharacterMarriages.RomanceRoutes.Single(
                value => value.RouteId == route.RouteId).Status);
        Assert.Equal(
            WorldState.GetCharacterConditionActionAffectedIds(resolved),
            campaignEvent.AffectedIds);

        CampaignEvent restored = SubmitCondition(
            simulation,
            new RestoreCharacterCapacityAction(
                Character(1),
                Condition(simulation, Character(1))),
            "restore-capacity");
        CharacterConditionChangedOutcome restoredOutcome = Assert.IsType<
            CharacterConditionChangedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(restored.Payload).Outcome);
        Assert.False(Condition(simulation, Character(1)).IsIncapacitated);
        Assert.Empty(restoredOutcome.MarriageChanges.InvalidatedProposals);
        Assert.Equal(
            MarriageProposalStatus.Invalidated,
            simulation.World.CharacterMarriages.Proposals.Single(
                value => value.ProposalId == proposal.ProposalId).Status);
    }

    [Fact]
    public void D302B_CustodyInvalidationAndReleaseDoNotResurrectConsentState()
    {
        MarriageProposalState proposal = ActiveProposal(
            "custody",
            Character(1),
            Character(2));
        RomanceInvitationState invitation = ActiveInvitation(
            "custody",
            Character(1),
            Character(3));
        RomanceRouteState route = LegacyRoute(
            "custody-active",
            Character(1),
            Character(4),
            RomanceRouteStatus.Active,
            2);
        CharacterMarriageWorldSnapshot marriages = new(
            CharacterMarriageContractVersions.Snapshot,
            [Practice()],
            [proposal],
            [],
            [],
            [route],
            [],
            [invitation]);
        CampaignSimulation simulation = CreateSimulation(6, marriages: marriages);

        CampaignEvent entered = SubmitCondition(
            simulation,
            new EnterCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1)),
                CharacterCustodyStatus.Detained,
                Character(0)),
            "custody-invalidation");
        CharacterConditionActionResolvedEventPayload enteredPayload = Assert.IsType<
            CharacterConditionActionResolvedEventPayload>(entered.Payload);
        CharacterMarriageLifecycleChangeSet changes = Assert.IsType<
            CharacterConditionChangedOutcome>(enteredPayload.Outcome).MarriageChanges;
        Assert.Equal(proposal.ProposalId, Assert.Single(changes.InvalidatedProposals).ProposalId);
        Assert.Equal(invitation.InvitationId, Assert.Single(changes.CancelledInvitations).InvitationId);
        Assert.Equal(route.RouteId, Assert.Single(changes.InvalidatedRomanceRoutes).RouteId);
        AssertHarmfulConsequence(
            Assert.IsType<RelationshipMemoryConsequenceSpecification>(
                enteredPayload.RelationshipMemoryConsequence),
            Character(1),
            Character(0));
        AssertRelationshipMemory(
            simulation,
            Character(1),
            Character(0),
            RelationshipMemorySourceKind.CharacterCondition,
            entered.EventId,
            new EntityId("memory_meaning:condition/entered_custody"));
        Assert.Equal(
            WorldState.GetCharacterConditionActionAffectedIds(enteredPayload, entered.EventId),
            entered.AffectedIds);

        CampaignEvent released = SubmitCondition(
            simulation,
            new ReleaseCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1))),
            "custody-release-no-resurrection");
        CharacterMarriageLifecycleChangeSet releaseChanges = Assert.IsType<
            CharacterConditionChangedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(released.Payload).Outcome)
            .MarriageChanges;
        Assert.Empty(releaseChanges.InvalidatedProposals);
        Assert.Equal(
            MarriageProposalStatus.Invalidated,
            simulation.World.CharacterMarriages.Proposals.Single().Status);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Equal(
            RomanceRouteStatus.Invalidated,
            simulation.World.CharacterMarriages.RomanceRoutes.Single().Status);
    }

    [Fact]
    public void D303_HouseholdExpulsionAndCaptiveIncorporationRecordHarmfulMemory()
    {
        CampaignSimulation expulsion = CreateSimulation(6);
        Assert.False(expulsion.Submit(HouseholdCommand(
            expulsion,
            Character(0),
            new ExpelHouseholdMemberAction(Household(0), Character(0)),
            "cannot-expel-head")).IsValid);
        CampaignEvent expelledEvent = SubmitHousehold(
            expulsion,
            Character(0),
            new ExpelHouseholdMemberAction(Household(0), Character(1)),
            "expel-member");
        HouseholdDecisionResolvedEventPayload expelled = Assert.IsType<
            HouseholdDecisionResolvedEventPayload>(expelledEvent.Payload);
        HouseholdMembershipTransition expulsionTransition = Assert.IsType<
            HouseholdMembershipChangedOutcome>(expelled.Outcome).Transition;

        Assert.Equal(HouseholdDecisionKind.Expulsion, expulsionTransition.Kind);
        Assert.Null(Profile(expulsion, Character(1)).HouseholdId);
        Assert.Equal(Household(0), expulsionTransition.SourceHouseholdId);
        Assert.Null(expulsionTransition.DestinationHouseholdId);
        AssertHarmfulConsequence(
            expelled.RelationshipMemoryConsequence,
            Character(1),
            Character(0));
        AssertRelationshipMemory(
            expulsion,
            Character(1),
            Character(0),
            RelationshipMemorySourceKind.HouseholdDecision,
            expelledEvent.EventId,
            new EntityId("memory_meaning:household/expulsion"));
        Assert.Equal(
            WorldState.GetHouseholdDecisionAffectedIds(expelled, expelledEvent.EventId),
            expelledEvent.AffectedIds);

        IReadOnlyDictionary<EntityId, CharacterConditionState> captiveCondition =
            new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(3)] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Character(0),
                },
            };
        CampaignSimulation incorporation = CreateSimulation(
            6,
            conditions: captiveCondition);
        CampaignSimulation freeTarget = CreateSimulation(6);
        Assert.False(freeTarget.Submit(HouseholdCommand(
            freeTarget,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
            "free-target")).IsValid);
        CampaignCommand wrongHead = HouseholdCommand(
            incorporation,
            Character(2),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
            "wrong-head");
        Assert.False(incorporation.Submit(wrongHead).IsValid);

        CampaignSimulation wrongCustodian = CreateSimulation(
            6,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(3)] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Character(2),
                },
            });
        Assert.False(wrongCustodian.Submit(HouseholdCommand(
            wrongCustodian,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
            "wrong-custodian")).IsValid);

        CampaignSimulation sameHousehold = CreateSimulation(
            6,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(1)] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Hostage,
                    CustodianId = Character(0),
                },
            });
        Assert.False(sameHousehold.Submit(HouseholdCommand(
            sameHousehold,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(1)),
            "same-household")).IsValid);

        CampaignSimulation sourceHead = CreateSimulation(
            6,
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(2)] = CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Character(0),
                },
            });
        Assert.False(sourceHead.Submit(HouseholdCommand(
            sourceHead,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(2)),
            "cannot-move-source-head")).IsValid);

        CampaignEvent incorporatedEvent = SubmitHousehold(
            incorporation,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
            "incorporate-captive");
        HouseholdDecisionResolvedEventPayload incorporated = Assert.IsType<
            HouseholdDecisionResolvedEventPayload>(incorporatedEvent.Payload);
        HouseholdMembershipTransition incorporationTransition = Assert.IsType<
            HouseholdMembershipChangedOutcome>(incorporated.Outcome).Transition;

        Assert.Equal(HouseholdDecisionKind.CaptiveIncorporation, incorporationTransition.Kind);
        Assert.Equal(Household(1), incorporationTransition.SourceHouseholdId);
        Assert.Equal(Household(0), incorporationTransition.DestinationHouseholdId);
        Assert.Equal(Household(0), Profile(incorporation, Character(3)).HouseholdId);
        Assert.Equal(CharacterCustodyStatus.Captive, Condition(incorporation, Character(3)).CustodyStatus);
        AssertRelationshipMemory(
            incorporation,
            Character(3),
            Character(0),
            RelationshipMemorySourceKind.HouseholdDecision,
            incorporatedEvent.EventId,
            new EntityId("memory_meaning:household/captive_incorporation"));
    }

    [Fact]
    public void D304_ExactCustodianCanImposeAtomicCoercedUnionWithoutPositiveRomance()
    {
        CampaignSimulation simulation = CreateSimulation(6);
        Assert.False(simulation.Submit(MarriageCommand(
            simulation,
            Character(2),
            new ImposeCoercedUnionAction(
                Character(1),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "coercion-before-custody")).IsValid);

        _ = SubmitCondition(
            simulation,
            new EnterCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1)),
                CharacterCustodyStatus.Captive,
                Character(0)),
            "enter-custody");
        Assert.False(simulation.Submit(MarriageCommand(
            simulation,
            Character(2),
            new ImposeCoercedUnionAction(
                Character(1),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "wrong-custodian")).IsValid);

        EntityId householdBefore = Profile(simulation, Character(1)).HouseholdId!.Value;
        CampaignEvent campaignEvent = SubmitMarriage(
            simulation,
            Character(0),
            new ImposeCoercedUnionAction(
                Character(1),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "impose-coerced-union");
        CharacterMarriageActionResolvedEventPayload resolved = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(campaignEvent.Payload);
        CoercedPoliticalUnionImposedOutcome outcome = Assert.IsType<
            CoercedPoliticalUnionImposedOutcome>(resolved.Outcome);

        Assert.Equal(MarriageProposalStatus.Accepted, outcome.Proposal.Status);
        Assert.Equal(MarriageBasis.Political, outcome.Proposal.Basis);
        Assert.Equal(MarriageConsentKind.Coerced, outcome.Proposal.ConsentKind);
        Assert.Equal(MarriageUnionStatus.Active, outcome.Union.Status);
        Assert.Equal(MarriageBasis.Political, outcome.Union.Basis);
        Assert.Equal(MarriageConsentKind.Coerced, outcome.Union.ConsentKind);
        Assert.Null(outcome.InvalidatedRomanceRoute);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Equal(householdBefore, Profile(simulation, Character(1)).HouseholdId);
        Assert.NotNull(resolved.RelationshipMemoryConsequence);
        AssertHarmfulConsequence(
            resolved.RelationshipMemoryConsequence!,
            Character(1),
            Character(0));
        AssertRelationshipMemory(
            simulation,
            Character(1),
            Character(0),
            RelationshipMemorySourceKind.CharacterMarriageAction,
            campaignEvent.EventId,
            new EntityId("memory_meaning:marriage/coerced_union"));
        Assert.Equal(
            WorldState.GetCharacterMarriageActionAffectedIds(resolved, campaignEvent.EventId),
            campaignEvent.AffectedIds);

        _ = SubmitCondition(
            simulation,
            new ReleaseCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1))),
            "release-custody");
        _ = SubmitCondition(
            simulation,
            new IncapacitateCharacterAction(
                Character(1),
                Condition(simulation, Character(1))),
            "incapacitate-coerced-recipient");
        Assert.Equal(
            MarriageProposalStatus.Accepted,
            simulation.World.CharacterMarriages.Proposals.Single().Status);
        Assert.Equal(
            MarriageUnionStatus.Active,
            simulation.World.CharacterMarriages.Unions.Single().Status);
    }

    [Fact]
    public void D304B_CoercedUnionInvalidatesOnlyTheActivePairRomanceRoute()
    {
        RomanceRouteState active = LegacyRoute(
            "coercion-active",
            Character(0),
            Character(1),
            RomanceRouteStatus.Active,
            2);
        RomanceRouteState completed = LegacyRoute(
            "coercion-completed",
            Character(0),
            Character(1),
            RomanceRouteStatus.Completed,
            4);
        RomanceRouteState ended = LegacyRoute(
            "coercion-ended",
            Character(0),
            Character(1),
            RomanceRouteStatus.Ended,
            3);
        CharacterMarriageWorldSnapshot marriages = new(
            CharacterMarriageContractVersions.Snapshot,
            [Practice()],
            [],
            [],
            [],
            [active, completed, ended],
            []);
        CampaignSimulation simulation = CreateSimulation(6, marriages: marriages);
        EntityId custodyCommandId = new("command:d3/coercion-legacy-custody-setup");
        EntityId custodyEventId = CharacterConditionIds.DeriveActionEventId(
            simulation.World.Calendar.Date,
            custodyCommandId);
        CharacterConditionMutationPlan custody =
            simulation.World.Characters.PrepareConditionAction(
                new EnterCharacterCustodyAction(
                    Character(1),
                    Condition(simulation, Character(1)),
                    CharacterCustodyStatus.Captive,
                    Character(0)),
                simulation.World.Calendar.Date,
                simulation.World.Calendar.TurnIndex,
                custodyCommandId,
                custodyEventId);
        // This deliberately emulates a legacy/inconsistent pre-D3 aggregate so the
        // coercive command's defensive active-route cleanup is directly exercised.
        simulation.World.Characters.ApplyPrepared(custody.CharacterPlan);

        CampaignEvent campaignEvent = SubmitMarriage(
            simulation,
            Character(0),
            new ImposeCoercedUnionAction(
                Character(1),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "coercion-invalidates-active-route");
        CoercedPoliticalUnionImposedOutcome outcome = Assert.IsType<
            CoercedPoliticalUnionImposedOutcome>(Assert.IsType<
                CharacterMarriageActionResolvedEventPayload>(campaignEvent.Payload).Outcome);

        Assert.Equal(active.RouteId, outcome.InvalidatedRomanceRoute?.RouteId);
        Assert.Equal(
            RomanceRouteStatus.Invalidated,
            simulation.World.CharacterMarriages.RomanceRoutes.Single(
                route => route.RouteId == active.RouteId).Status);
        Assert.Equal(
            completed,
            simulation.World.CharacterMarriages.RomanceRoutes.Single(
                route => route.RouteId == completed.RouteId));
        Assert.Equal(
            ended,
            simulation.World.CharacterMarriages.RomanceRoutes.Single(
                route => route.RouteId == ended.RouteId));
    }

    [Fact]
    public void D304C_HarmfulConsequenceSaturationLadderIsExactAndNeverChangesAttraction()
    {
        (RelationshipImpact Seed, RelationshipImpact Expected)[] scenarios =
        [
            (new(0, 0, 0, 0, 0, 0, 0, 0, 0),
                new(0, 0, 0, 0, 0, 0, 25, 0, 0)),
            (new(0, 0, 0, 0, 0, 0, 100, 0, 0),
                new(0, 0, 0, 0, 0, 25, 0, 0, 0)),
            (new(0, 0, 0, 0, 0, 100, 100, 0, 0),
                new(0, 0, 0, 0, 0, 0, 0, 25, 0)),
            (new(0, 40, 0, 0, 0, 100, 100, 100, 0),
                new(0, -25, 0, 0, 0, 0, 0, 0, 0)),
            (new(40, 0, 0, 0, 0, 100, 100, 100, 0),
                new(-25, 0, 0, 0, 0, 0, 0, 0, 0)),
            (new(0, 0, 40, 0, 0, 100, 100, 100, 0),
                new(0, 0, -25, 0, 0, 0, 0, 0, 0)),
            (new(0, 0, 0, 0, 0, 100, 100, 100, 0),
                new(0, 0, 0, 0, 0, 0, 0, 0, -25)),
            (new(0, 0, 0, 50, 0, 100, 100, 100, -100),
                new(0, 0, 0, 0, 0, 0, 0, 0, 0)),
        ];

        for (int index = 0; index < scenarios.Length; index++)
        {
            CampaignSimulation simulation = CreateSimulation(6);
            if (scenarios[index].Seed.HasAnyChange)
            {
                CampaignCommand seed = CampaignCommand.Create(
                    new EntityId($"command:d3/harmful-ladder-seed-{index}"),
                    Character(1),
                    simulation.World.Calendar.Date,
                    new RelationshipActionCommandPayload(
                        Character(0),
                        scenarios[index].Seed,
                        new EntityId("memory_meaning:d3/harmful_ladder_seed"),
                        1,
                        MemoryPublicity.Private,
                        0,
                        []));
                AssertValid(simulation.Submit(seed));
                Assert.Single(simulation.ResolveTurn());
            }

            EntityId eventId = new($"event:d3/harmful-ladder-{index}");
            EntityId consequenceId = HouseholdDecisionIds.DeriveRelationshipConsequenceId(
                eventId,
                0);
            RelationshipMemoryConsequenceSpecification consequence =
                simulation.World.Relationships.PlanHarmfulConsequence(
                    eventId,
                    consequenceId,
                    Character(1),
                    Character(0),
                    new EntityId("memory_meaning:d3/harmful_ladder"),
                    simulation.World.Calendar.Date,
                    simulation.World.Calendar.TurnIndex);

            Assert.Equal(consequenceId, consequence.ConsequenceId);
            Assert.Equal(scenarios[index].Expected, consequence.Impact);
            Assert.Equal(0, consequence.Impact.Attraction);
            Assert.Equal(new EntityId("memory_meaning:d3/harmful_ladder"), consequence.MeaningId);
            Assert.Equal(75, consequence.InitialSeverity);
            Assert.Equal(MemoryPublicity.Participants, consequence.Publicity);
            Assert.Equal(0, consequence.DecayIntervalTurns);
            Assert.Empty(consequence.WitnessIds);
        }
    }

    [Fact]
    public void D305_InternalDeathPreviewPlansCompleteMarriageLifecycleWithoutMutation()
    {
        CampaignSimulation simulation = CreateSimulation(12);
        MarriageUnionState firstUnion = CreateDirectUnion(
            simulation,
            Character(1),
            Character(6),
            "death-union-one");
        MarriageUnionState secondUnion = CreateDirectUnion(
            simulation,
            Character(1),
            Character(7),
            "death-union-two");
        MarriageProposalState activeProposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new ProposePoliticalMarriageAction(
                    Character(2),
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "death-active-proposal").Payload).Outcome).Proposal;
        RomanceInvitationState activeInvitation = Assert.IsType<RomanceInvitationCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new OfferRomanceRouteAction(Character(3), PracticeId),
                "death-active-invitation").Payload).Outcome).Invitation;
        RomanceRouteState activeV2Route = CreateActiveRomanceRoute(
            simulation,
            Character(1),
            Character(4),
            "death-active-v2-route");
        MarriageProposalState betrothalProposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new ProposePoliticalMarriageAction(
                    Character(5),
                    MarriageProposalKind.PoliticalBetrothal,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "death-active-betrothal-offer").Payload).Outcome).Proposal;
        PoliticalBetrothalState activeBetrothal = Assert.IsType<PoliticalBetrothalAcceptedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(5),
                new RespondToPoliticalMarriageProposalAction(
                    betrothalProposal.ProposalId,
                    MarriageProposalResponse.Accept),
                "death-active-betrothal-accept").Payload).Outcome).Betrothal;
        MarriageProposalState terminalProposalSource = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new ProposePoliticalMarriageAction(
                    Character(8),
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "death-terminal-proposal-create").Payload).Outcome).Proposal;
        MarriageProposalState terminalProposal = Assert.IsType<MarriageProposalWithdrawnOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new WithdrawPoliticalMarriageProposalAction(terminalProposalSource.ProposalId),
                "death-terminal-proposal-withdraw").Payload).Outcome).Proposal;
        RomanceRouteState terminalRouteSource = CreateActiveRomanceRoute(
            simulation,
            Character(1),
            Character(9),
            "death-terminal-route");
        RomanceRouteState terminalRoute = Assert.IsType<RomanceRouteEndedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                Character(1),
                new EndRomanceRouteAction(terminalRouteSource.RouteId),
                "death-terminal-route-end").Payload).Outcome).Route;
        RomanceRouteState activeV1Route = LegacyRoute(
            "death-active-v1-route",
            Character(1),
            Character(10),
            RomanceRouteStatus.Active,
            2);
        WorldSnapshot seeded = simulation.World.CaptureSnapshot();
        simulation = new CampaignSimulation(WorldState.Restore(seeded with
        {
            CharacterMarriages = seeded.CharacterMarriages with
            {
                RomanceRoutes = seeded.CharacterMarriages.RomanceRoutes
                    .Append(activeV1Route)
                    .ToArray(),
            },
        }));
        CharacterConditionState before = Condition(simulation, Character(1));
        string beforeSnapshot = SnapshotJson(simulation);
        EntityId commandId = new("command:d3/death-preview");
        EntityId eventId = CharacterConditionIds.DeriveActionEventId(
            simulation.World.Calendar.Date,
            commandId);

        CharacterDeathPreviewAggregatePlan preview =
            simulation.World.PrepareCharacterDeathPreview(
                Character(1),
                before,
                simulation.World.Calendar.Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);

        Assert.Equal(CharacterVitalStatus.Dead, preview.Change.CurrentCondition.VitalStatus);
        Assert.Equal(
            activeProposal.ProposalId,
            Assert.Single(preview.MarriageChanges.InvalidatedProposals).ProposalId);
        Assert.Equal(
            activeBetrothal.BetrothalId,
            Assert.Single(preview.MarriageChanges.InvalidatedBetrothals).BetrothalId);
        Assert.Equal(
            activeInvitation.InvitationId,
            Assert.Single(preview.MarriageChanges.CancelledInvitations).InvitationId);
        Assert.Equal(
            new[] { activeV2Route.RouteId, activeV1Route.RouteId }.Order().ToArray(),
            preview.MarriageChanges.InvalidatedRomanceRoutes
                .Select(route => route.RouteId)
                .Order()
                .ToArray());
        Assert.Equal(
            new[] { firstUnion.UnionId, secondUnion.UnionId }.Order().ToArray(),
            preview.MarriageChanges.EndedUnions.Select(union => union.UnionId).Order().ToArray());
        Assert.All(preview.MarriageChanges.EndedUnions, union =>
        {
            Assert.Equal(MarriageUnionStatus.Ended, union.Status);
            Assert.Equal(MarriageUnionEndReason.SpouseDied, union.EndReason);
            Assert.Equal(commandId, union.EndCommandId);
        });
        RomanceRouteState invalidatedV2 = preview.MarriageChanges.InvalidatedRomanceRoutes.Single(
            route => route.RouteId == activeV2Route.RouteId);
        Assert.Equal(activeV2Route.SourceInvitationId, invalidatedV2.SourceInvitationId);
        Assert.Equal(
            activeV2Route.InvitationSourceCommandId,
            invalidatedV2.InvitationSourceCommandId);
        Assert.Equal(
            activeV2Route.LastPositiveProgressCommandId,
            invalidatedV2.LastPositiveProgressCommandId);
        CharacterMarriageWorldSnapshot candidate = preview.MarriagePlan.Candidate.CaptureSnapshot();
        Assert.Equal(
            terminalProposal,
            candidate.Proposals.Single(item => item.ProposalId == terminalProposal.ProposalId));
        Assert.Equal(
            terminalRoute,
            candidate.RomanceRoutes.Single(item => item.RouteId == terminalRoute.RouteId));
        Assert.Equal(before, Condition(simulation, Character(1)));
        Assert.Equal(beforeSnapshot, SnapshotJson(simulation));
        Assert.All(simulation.World.CharacterMarriages.Unions, union =>
            Assert.Equal(MarriageUnionStatus.Active, union.Status));
    }

    [Fact]
    public void D306_TamperedHouseholdEventRollsBackEverySubsystem()
    {
        CampaignSimulation simulation = CreateSimulation(6);
        EntityId commandId = new("command:d3/tamper-household");
        EntityId eventId = HouseholdDecisionIds.DeriveActionEventId(
            simulation.World.Calendar.Date,
            commandId);
        HouseholdDecisionAggregatePlan aggregate = simulation.World.PrepareHouseholdDecision(
            Character(0),
            new HouseholdDecisionCommandPayload(
                new ExpelHouseholdMemberAction(Household(0), Character(1))),
            simulation.World.Calendar.Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        string before = SnapshotJson(simulation);
        CampaignEvent forgedAffected = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            simulation.World.Calendar.Date,
            ResolutionPhase.Commands,
            0,
            [],
            aggregate.ResolvedPayload);

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(forgedAffected));
        Assert.Equal(before, SnapshotJson(simulation));

        RelationshipMemoryConsequenceSpecification consequence =
            aggregate.ResolvedPayload.RelationshipMemoryConsequence;
        HouseholdDecisionResolvedEventPayload forgedPayload = aggregate.ResolvedPayload with
        {
            RelationshipMemoryConsequence = consequence with
            {
                Impact = consequence.Impact with { Affection = 1 },
            },
        };
        CampaignEvent forgedConsequence = forgedAffected with
        {
            AffectedIds = WorldState.GetHouseholdDecisionAffectedIds(
                forgedPayload,
                eventId),
            Payload = forgedPayload,
        };

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(forgedConsequence));
        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(Household(0), Profile(simulation, Character(1)).HouseholdId);
        Assert.Empty(simulation.World.Relationships.Subjects);
    }

    [Fact]
    public void D307_PendingD3ReplayAndCurrentSaveAreDeterministic()
    {
        CampaignSimulation original = CreateSimulation(6);
        CharacterConditionState expected = Condition(original, Character(1));
        CampaignCommand condition = CampaignCommand.Create(
            new EntityId("command:d3/replay-condition"),
            CharacterConditionSystem.AuthoritativeActorId,
            original.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new IncapacitateCharacterAction(Character(1), expected)),
            priority: 0);
        CampaignCommand household = CampaignCommand.Create(
            new EntityId("command:d3/replay-household"),
            Character(0),
            original.World.Calendar.Date,
            new HouseholdDecisionCommandPayload(
                new ExpelHouseholdMemberAction(Household(0), Character(1))),
            priority: 2);
        CampaignCommand staleCustody = CampaignCommand.Create(
            new EntityId("command:d3/replay-stale-custody"),
            CharacterConditionSystem.AuthoritativeActorId,
            original.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new EnterCharacterCustodyAction(
                    Character(1),
                    expected,
                    CharacterCustodyStatus.Detained,
                    Character(0))),
            priority: 1);
        Assert.True(original.Submit(condition).IsValid);
        Assert.True(original.Submit(staleCustody).IsValid);
        Assert.True(original.Submit(household).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-d3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "d3-pending.save.gz");
            SaveEnvelope envelope = SaveEnvelope.Create("test", [], original);
            new SaveStore().SaveAtomic(path, envelope);
            SaveEnvelope loaded = new SaveStore().Load(path);
            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal(3, loaded.Snapshot.PendingCommands.Count);
            CampaignSimulation replay = new(WorldState.Restore(loaded.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();

            Assert.Equal(
                JsonSerializer.Serialize(first, SimulationJson.CreateOptions()),
                JsonSerializer.Serialize(second, SimulationJson.CreateOptions()));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
            Assert.Single(first, campaignEvent => campaignEvent.Payload
                is CommandCancelledEventPayload
            {
                ReasonCode: "command_invalidated",
            });
            Assert.Equal(
                CharacterCustodyStatus.Free,
                Condition(original, Character(1)).CustodyStatus);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void D307B_CurrentSaveRoundTripsEveryD3ActionAndSourceKind()
    {
        CampaignSimulation simulation = CreateSimulation(10);
        RomanceRouteState route = CreateActiveRomanceRoute(
            simulation,
            Character(0),
            Character(4),
            "save-v2-route");
        _ = SubmitCondition(
            simulation,
            new EnterCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1)),
                CharacterCustodyStatus.Detained,
                Character(0)),
            "save-enter-custody-one");
        _ = SubmitCondition(
            simulation,
            new ReleaseCharacterCustodyAction(
                Character(1),
                Condition(simulation, Character(1))),
            "save-release-custody");
        _ = SubmitCondition(
            simulation,
            new IncapacitateCharacterAction(
                Character(2),
                Condition(simulation, Character(2))),
            "save-incapacitate");
        _ = SubmitCondition(
            simulation,
            new RestoreCharacterCapacityAction(
                Character(2),
                Condition(simulation, Character(2))),
            "save-restore-capacity");
        _ = SubmitHousehold(
            simulation,
            Character(0),
            new ExpelHouseholdMemberAction(Household(0), Character(1)),
            "save-expulsion");
        _ = SubmitCondition(
            simulation,
            new EnterCharacterCustodyAction(
                Character(3),
                Condition(simulation, Character(3)),
                CharacterCustodyStatus.Captive,
                Character(0)),
            "save-enter-custody-three");
        _ = SubmitHousehold(
            simulation,
            Character(0),
            new IncorporateCaptiveHouseholdMemberAction(Household(0), Character(3)),
            "save-incorporation");
        _ = SubmitCondition(
            simulation,
            new EnterCharacterCustodyAction(
                Character(4),
                Condition(simulation, Character(4)),
                CharacterCustodyStatus.Hostage,
                Character(0)),
            "save-enter-custody-four");
        _ = SubmitMarriage(
            simulation,
            Character(0),
            new ImposeCoercedUnionAction(
                Character(4),
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "save-coerced-union");

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-d3-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "d3-current.save.gz");
            new SaveStore().SaveAtomic(
                path,
                SaveEnvelope.Create("test", [], simulation));
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            ICharacterConditionAction[] conditionActions = loaded.DiagnosticCommands
                .Select(command => command.Payload)
                .OfType<CharacterConditionActionCommandPayload>()
                .Select(payload => payload.Action)
                .ToArray();
            Assert.Contains(conditionActions, action => action is IncapacitateCharacterAction);
            Assert.Contains(conditionActions, action => action is RestoreCharacterCapacityAction);
            Assert.Contains(conditionActions, action => action is EnterCharacterCustodyAction);
            Assert.Contains(conditionActions, action => action is ReleaseCharacterCustodyAction);
            Assert.Contains(
                loaded.DiagnosticCommands,
                command => command.Payload is HouseholdDecisionCommandPayload
                {
                    Action: ExpelHouseholdMemberAction,
                });
            Assert.Contains(
                loaded.DiagnosticCommands,
                command => command.Payload is HouseholdDecisionCommandPayload
                {
                    Action: IncorporateCaptiveHouseholdMemberAction,
                });
            Assert.Contains(
                loaded.DiagnosticCommands,
                command => command.Payload is CharacterMarriageActionCommandPayload
                {
                    Action: ImposeCoercedUnionAction,
                });
            Assert.Contains(
                loaded.DiagnosticEvents,
                campaignEvent => campaignEvent.Payload
                    is CharacterMarriageActionResolvedEventPayload
                {
                    Outcome: CoercedPoliticalUnionImposedOutcome,
                });
            RelationshipMemorySourceKind[] sources = loaded.Snapshot.Relationships.Subjects
                .SelectMany(subject => subject.DetailedRelationships)
                .SelectMany(relationship => relationship.Memories)
                .Select(memory => memory.SourceKind)
                .ToArray();
            Assert.Contains(RelationshipMemorySourceKind.CharacterCondition, sources);
            Assert.Contains(RelationshipMemorySourceKind.HouseholdDecision, sources);
            Assert.Contains(RelationshipMemorySourceKind.CharacterMarriageAction, sources);
            Assert.Equal(
                RomanceRouteStatus.Invalidated,
                loaded.Snapshot.CharacterMarriages.RomanceRoutes.Single(
                    value => value.RouteId == route.RouteId).Status);
            Assert.Contains(
                loaded.Snapshot.CharacterMarriages.Unions,
                union => union.ConsentKind == MarriageConsentKind.Coerced);
            _ = WorldState.Restore(loaded.Snapshot);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void D307C_SamePriorityConditionRaceIsDeterministicInBothEventIdOrders()
    {
        EntityId firstCommandId = new("command:d3/race-order-first");
        EntityId secondCommandId = new("command:d3/race-order-second");
        if (CharacterConditionIds.DeriveActionEventId(Date, secondCommandId)
            .CompareTo(CharacterConditionIds.DeriveActionEventId(Date, firstCommandId)) < 0)
        {
            (firstCommandId, secondCommandId) = (secondCommandId, firstCommandId);
        }

        for (int scenario = 0; scenario < 2; scenario++)
        {
            CampaignSimulation simulation = CreateSimulation(6);
            CharacterConditionState expected = Condition(simulation, Character(1));
            ICharacterConditionAction earlier = scenario == 0
                ? new IncapacitateCharacterAction(Character(1), expected)
                : new EnterCharacterCustodyAction(
                    Character(1),
                    expected,
                    CharacterCustodyStatus.Detained,
                    Character(0));
            ICharacterConditionAction later = scenario == 0
                ? new EnterCharacterCustodyAction(
                    Character(1),
                    expected,
                    CharacterCustodyStatus.Detained,
                    Character(0))
                : new IncapacitateCharacterAction(Character(1), expected);
            AssertValid(simulation.Submit(CampaignCommand.Create(
                firstCommandId,
                CharacterConditionSystem.AuthoritativeActorId,
                simulation.World.Calendar.Date,
                new CharacterConditionActionCommandPayload(earlier))));
            AssertValid(simulation.Submit(CampaignCommand.Create(
                secondCommandId,
                CharacterConditionSystem.AuthoritativeActorId,
                simulation.World.Calendar.Date,
                new CharacterConditionActionCommandPayload(later))));

            IReadOnlyList<CampaignEvent> resolved = simulation.ResolveTurn();

            Assert.IsType<CharacterConditionActionResolvedEventPayload>(resolved[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(resolved[1].Payload);
            Assert.Equal(
                scenario == 0,
                Condition(simulation, Character(1)).IsIncapacitated);
            Assert.Equal(
                scenario == 0
                    ? CharacterCustodyStatus.Free
                    : CharacterCustodyStatus.Detained,
                Condition(simulation, Character(1)).CustodyStatus);
        }
    }

    [Fact]
    public void D308_RelationshipOverflowCannotPartiallyApplyHouseholdOrMarriageState()
    {
        CampaignSimulation seed = CreateSimulation(194);
        for (int index = 2; index < 194; index++)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:d3/overflow-seed-{index:D3}"),
                Character(1),
                seed.World.Calendar.Date,
                new RelationshipActionCommandPayload(
                    Character(index),
                    new RelationshipImpact(1, 0, 0, 0, 0, 0, 0, 0, 0),
                    new EntityId("memory_meaning:d3/overflow_seed"),
                    1,
                    MemoryPublicity.Private,
                    0,
                    []));
            AssertValid(seed.Submit(command));
        }

        Assert.Equal(192, seed.ResolveTurn().Count);
        WorldSnapshot seeded = seed.World.CaptureSnapshot();
        SubjectRelationshipHistory history = Assert.Single(
            seeded.Relationships.Subjects,
            item => item.SubjectCharacterId == Character(1));
        Assert.Equal(64, history.DetailedRelationships.Count);
        Assert.Equal(128, history.ArchivedRelationships.Count);
        RelationshipWorldSnapshot nearOverflow = seeded.Relationships with
        {
            Subjects = seeded.Relationships.Subjects.Select(item =>
                item.SubjectCharacterId == Character(1)
                    ? item with
                    {
                        DistantHistory = new DistantRelationshipHistoryAggregate(
                            long.MaxValue,
                            long.MaxValue,
                            long.MaxValue,
                            long.MaxValue,
                            Date,
                            Date,
                            0),
                    }
                    : item).ToArray(),
        };
        CampaignSimulation simulation = new(WorldState.Create(
            seeded.Calendar.Date,
            seeded.RootSeed,
            [],
            seeded.Geography,
            seeded.Characters,
            nearOverflow,
            seeded.Careers,
            seeded.CharacterResources,
            seeded.CharacterEstateHoldings,
            seeded.CharacterMarriages));
        CharacterWorldState characterReference = simulation.World.Characters;
        CharacterMarriageWorldState marriageReference = simulation.World.CharacterMarriages;
        string before = SnapshotJson(simulation);
        EntityId commandId = new("command:d3/overflow-household");
        EntityId eventId = HouseholdDecisionIds.DeriveActionEventId(
            simulation.World.Calendar.Date,
            commandId);

        Assert.Throws<SimulationValidationException>(() => simulation.World.PrepareHouseholdDecision(
            Character(0),
            new HouseholdDecisionCommandPayload(
                new ExpelHouseholdMemberAction(Household(0), Character(1))),
            simulation.World.Calendar.Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId));

        Assert.Same(characterReference, simulation.World.Characters);
        Assert.Same(marriageReference, simulation.World.CharacterMarriages);
        Assert.Equal(before, SnapshotJson(simulation));
        Assert.Equal(Household(0), Profile(simulation, Character(1)).HouseholdId);
        Assert.Empty(simulation.World.CharacterMarriages.Proposals);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);

        CampaignSimulation coercion = new(WorldState.Create(
            seeded.Calendar.Date,
            seeded.RootSeed,
            [],
            seeded.Geography,
            seeded.Characters,
            nearOverflow,
            seeded.Careers,
            seeded.CharacterResources,
            seeded.CharacterEstateHoldings,
            seeded.CharacterMarriages));
        EntityId custodyCommandId = new("command:d3/overflow-custody-setup");
        EntityId custodyEventId = CharacterConditionIds.DeriveActionEventId(
            coercion.World.Calendar.Date,
            custodyCommandId);
        CharacterConditionMutationPlan custody = coercion.World.Characters.PrepareConditionAction(
            new EnterCharacterCustodyAction(
                Character(1),
                Condition(coercion, Character(1)),
                CharacterCustodyStatus.Captive,
                Character(0)),
            coercion.World.Calendar.Date,
            coercion.World.Calendar.TurnIndex,
            custodyCommandId,
            custodyEventId);
        coercion.World.Characters.ApplyPrepared(custody.CharacterPlan);
        string coercionBefore = SnapshotJson(coercion);
        EntityId coercionCommandId = new("command:d3/overflow-coerced-union");
        EntityId coercionEventId = CharacterMarriageIds.DeriveActionEventId(
            coercion.World.Calendar.Date,
            coercionCommandId);

        Assert.Throws<SimulationValidationException>(() =>
            coercion.World.PrepareCharacterMarriageAction(
                Character(0),
                new CharacterMarriageActionCommandPayload(new ImposeCoercedUnionAction(
                    Character(1),
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId)),
                coercion.World.Calendar.Date,
                coercion.World.Calendar.TurnIndex,
                coercionCommandId,
                coercionEventId));
        Assert.Equal(coercionBefore, SnapshotJson(coercion));
        Assert.Empty(coercion.World.CharacterMarriages.Proposals);
        Assert.Empty(coercion.World.CharacterMarriages.Unions);
    }

    [Fact]
    public void D309_ThousandCharacterCoercionAndLifecycleWorkflowRecordsRawPerformance()
    {
        IReadOnlyDictionary<EntityId, CharacterConditionState> conditions =
            Enumerable.Range(0, 200).ToDictionary(
                index => Character(500 + index),
                index => CharacterConditionState.Default with
                {
                    CustodyStatus = CharacterCustodyStatus.Captive,
                    CustodianId = Character(index),
                });
        CampaignSimulation simulation = CreateSimulation(1_000, conditions);

        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 0; index < 200; index++)
        {
            AssertValid(simulation.Submit(MarriageCommand(
                simulation,
                Character(index),
                new ImposeCoercedUnionAction(
                    Character(500 + index),
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                $"performance/coercion-{index:D3}")));
        }

        Assert.Equal(200, simulation.ResolveTurn().Count);
        for (int index = 0; index < 200; index++)
        {
            EntityId target = Character(500 + index);
            AssertValid(simulation.Submit(ConditionCommand(
                simulation,
                new ReleaseCharacterCustodyAction(target, Condition(simulation, target)),
                $"performance/release-{index:D3}")));
        }

        Assert.Equal(200, simulation.ResolveTurn().Count);
        for (int index = 0; index < 200; index++)
        {
            EntityId target = Character(700 + index);
            AssertValid(simulation.Submit(ConditionCommand(
                simulation,
                new IncapacitateCharacterAction(target, Condition(simulation, target)),
                $"performance/incapacitate-{index:D3}")));
        }

        Assert.Equal(200, simulation.ResolveTurn().Count);
        workflow.Stop();

        Stopwatch checksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        string checksumValue = SimulationChecksum.Compute(snapshot).Value;
        checksum.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, CanonicalJson.Options);
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.Equal(200, snapshot.CharacterMarriages.Proposals.Count);
        Assert.Equal(200, snapshot.CharacterMarriages.Unions.Count);
        Assert.Equal(
            200,
            snapshot.Characters.CharacterStates.Count(
                state => state.Condition?.IsIncapacitated == true));
        Assert.Equal(
            200,
            snapshot.Relationships.Subjects.Sum(subject =>
                subject.DetailedRelationships.Sum(relationship => relationship.Memories.Count)));
        Assert.All(
            snapshot.Relationships.Subjects.SelectMany(subject => subject.DetailedRelationships),
            relationship => Assert.Equal(0, relationship.Dimensions.Attraction));
        output.WriteLine(
            $"SP-04D3 raw fixture: characters=1000; coerced_unions=200; "
            + $"released_captives=200; incapacitated_characters=200; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={checksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksumValue}");
    }

    private static CampaignEvent SubmitCondition(
        CampaignSimulation simulation,
        ICharacterConditionAction action,
        string suffix)
    {
        CampaignCommand command = ConditionCommand(simulation, action, suffix);
        AssertValid(simulation.Submit(command));
        return Assert.Single(simulation.ResolveTurn());
    }

    private static CampaignCommand ConditionCommand(
        CampaignSimulation simulation,
        ICharacterConditionAction action,
        string suffix) => CampaignCommand.Create(
            new EntityId($"command:d3/{suffix}"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(action));

    private static CampaignEvent SubmitHousehold(
        CampaignSimulation simulation,
        EntityId actor,
        IHouseholdDecisionAction action,
        string suffix)
    {
        CampaignCommand command = HouseholdCommand(simulation, actor, action, suffix);
        AssertValid(simulation.Submit(command));
        return Assert.Single(simulation.ResolveTurn());
    }

    private static CampaignCommand HouseholdCommand(
        CampaignSimulation simulation,
        EntityId actor,
        IHouseholdDecisionAction action,
        string suffix) => CampaignCommand.Create(
            new EntityId($"command:d3/{suffix}"),
            actor,
            simulation.World.Calendar.Date,
            new HouseholdDecisionCommandPayload(action));

    private static CampaignEvent SubmitMarriage(
        CampaignSimulation simulation,
        EntityId actor,
        ICharacterMarriageAction action,
        string suffix)
    {
        CampaignCommand command = MarriageCommand(simulation, actor, action, suffix);
        AssertValid(simulation.Submit(command));
        return Assert.Single(simulation.ResolveTurn());
    }

    private static MarriageUnionState CreateDirectUnion(
        CampaignSimulation simulation,
        EntityId proposer,
        EntityId recipient,
        string suffix)
    {
        MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                proposer,
                new ProposePoliticalMarriageAction(
                    recipient,
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                $"{suffix}-offer").Payload).Outcome).Proposal;
        return Assert.IsType<DirectPoliticalUnionAcceptedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                recipient,
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Accept),
                $"{suffix}-accept").Payload).Outcome).Union;
    }

    private static RomanceRouteState CreateActiveRomanceRoute(
        CampaignSimulation simulation,
        EntityId initiator,
        EntityId recipient,
        string suffix)
    {
        RomanceInvitationState invitation = Assert.IsType<RomanceInvitationCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                initiator,
                new OfferRomanceRouteAction(recipient, PracticeId),
                $"{suffix}-offer").Payload).Outcome).Invitation;
        return Assert.IsType<RomanceRouteStartedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitMarriage(
                simulation,
                recipient,
                new RespondToRomanceInvitationAction(
                    invitation.InvitationId,
                    RomanceInvitationResponse.Accept),
                $"{suffix}-accept").Payload).Outcome).Route;
    }

    private static CampaignCommand MarriageCommand(
        CampaignSimulation simulation,
        EntityId actor,
        ICharacterMarriageAction action,
        string suffix) => CampaignCommand.Create(
            new EntityId($"command:d3/{suffix}"),
            actor,
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(action));

    private static void AssertValid(CommandValidationResult result) => Assert.True(
        result.IsValid,
        string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));

    private static void AssertHarmfulConsequence(
        RelationshipMemoryConsequenceSpecification consequence,
        EntityId subject,
        EntityId target)
    {
        Assert.Equal(subject, consequence.SubjectCharacterId);
        Assert.Equal(target, consequence.TargetCharacterId);
        Assert.Equal(
            new RelationshipImpact(0, 0, 0, 0, 0, 0, 25, 0, 0),
            consequence.Impact);
        Assert.Equal(75, consequence.InitialSeverity);
        Assert.Equal(MemoryPublicity.Participants, consequence.Publicity);
        Assert.Equal(0, consequence.DecayIntervalTurns);
        Assert.Empty(consequence.WitnessIds);
    }

    private static void AssertRelationshipMemory(
        CampaignSimulation simulation,
        EntityId subject,
        EntityId target,
        RelationshipMemorySourceKind expectedSource,
        EntityId sourceEventId,
        EntityId meaningId)
    {
        Assert.True(simulation.World.Relationships.TryGetSubjectHistory(
            subject,
            out SubjectRelationshipHistory? history));
        DetailedDirectionalRelationship relationship = Assert.Single(
            history.DetailedRelationships,
            value => value.TargetCharacterId == target);
        ConsequentialMemory memory = Assert.Single(
            relationship.Memories,
            value => value.SourceEventId == sourceEventId);
        Assert.Equal(expectedSource, memory.SourceKind);
        Assert.Equal(sourceEventId, memory.SourceEventId);
        Assert.Equal(meaningId, memory.MeaningId);
        Assert.Equal(
            RelationshipIds.DeriveMemoryId(sourceEventId, subject, target, 0),
            memory.MemoryId);
        Assert.Equal(RelationshipMemoryIdentityScheme.SourceEventV2, memory.IdentityScheme);
        Assert.Equal(0, memory.ConsequenceIndex);
        Assert.Equal(simulation.World.Calendar.TurnIndex - 1, memory.RecordedTurnIndex);
        Assert.Equal(75, memory.InitialSeverity);
        Assert.Equal(MemoryPublicity.Participants, memory.Publicity);
        Assert.Equal(0, memory.DecayIntervalTurns);
        Assert.Empty(memory.WitnessIds);
        Assert.Equal(0, memory.AppliedImpact.Attraction);
    }

    private static CharacterConditionState Condition(
        CampaignSimulation simulation,
        EntityId characterId) => Profile(simulation, characterId).Condition;

    private static AuthoritativeCharacterProfile Profile(
        CampaignSimulation simulation,
        EntityId characterId)
    {
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            characterId,
            out AuthoritativeCharacterProfile? profile));
        return profile;
    }

    private static string SnapshotJson(CampaignSimulation simulation) =>
        JsonSerializer.Serialize(
            simulation.World.CaptureSnapshot(),
            SimulationJson.CreateOptions());

    private static CampaignSimulation CreateSimulation(
        int characterCount,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null,
        RelationshipWorldSnapshot? relationships = null,
        CharacterMarriageWorldSnapshot? marriages = null)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, characterCount)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:d3/character_{index}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    new CampaignDate(170, 1, 1),
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
        HouseholdDefinition[] householdDefinitions = characterCount >= 4
            ?
            [
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    Household(0),
                    new EntityId("loc:d3/household_0")),
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    Household(1),
                    new EntityId("loc:d3/household_1")),
            ]
            : [];
        HouseholdState[] householdStates = characterCount >= 4
            ?
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    Household(0),
                    Character(0),
                    [Character(0), Character(1)]),
                new HouseholdState(
                    CharacterContractVersions.State,
                    Household(1),
                    Character(2),
                    [Character(2), Character(3)]),
            ]
            : [];
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            householdDefinitions,
            definitions.Select(definition => new CharacterState(
                CharacterContractVersions.State,
                definition.Id,
                [],
                [],
                conditions is not null
                    && conditions.TryGetValue(
                        definition.Id,
                        out CharacterConditionState? condition)
                    && condition is not null
                        ? condition
                        : CharacterConditionState.Default)).ToArray(),
            [],
            householdStates);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            relationships ?? RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            marriages ?? new CharacterMarriageWorldSnapshot(
                CharacterMarriageContractVersions.Snapshot,
                [Practice()],
                [],
                [],
                [],
                [],
                [])));
    }

    private static MarriageProposalState ActiveProposal(
        string suffix,
        EntityId proposer,
        EntityId recipient) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_proposal:d3/{suffix}"),
        MarriageProposalKind.LegalUnion,
        MarriageBasis.Political,
        MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind.PoliticalArrangement,
        proposer,
        recipient,
        null,
        PracticeId,
        Date.AddDays(-1),
        0,
        new EntityId($"command:d3/{suffix}-proposal"),
        MarriageProposalStatus.Active,
        null,
        null,
        null);

    private static RomanceInvitationState ActiveInvitation(
        string suffix,
        EntityId initiator,
        EntityId recipient)
    {
        EntityId commandId = new($"command:d3/{suffix}-invitation");
        CampaignDate created = Date.AddDays(-1);
        return new RomanceInvitationState(
            CharacterMarriageContractVersions.RomanceInvitationState,
            CharacterMarriageIds.DeriveRomanceInvitationId(created, commandId),
            initiator,
            recipient,
            PracticeId,
            created,
            0,
            commandId);
    }

    private static RomanceRouteState LegacyRoute(
        string suffix,
        EntityId first,
        EntityId second,
        RomanceRouteStatus status,
        int progress)
    {
        CampaignDate start = Date.AddDays(-3);
        bool active = status == RomanceRouteStatus.Active;
        return new RomanceRouteState(
            CharacterMarriageContractVersions.State,
            new EntityId($"romance_route:d3/{suffix}"),
            first,
            second,
            PracticeId,
            progress,
            start,
            0,
            new EntityId($"command:d3/{suffix}-route"),
            status,
            active ? null : Date.AddDays(-1),
            active ? null : 0,
            active ? null : new EntityId($"command:d3/{suffix}-resolution"));
    }

    private static MarriagePracticeState Practice() => new(
        CharacterMarriageContractVersions.Practice,
        PracticeId,
        18,
        18,
        8,
        64,
        64,
        true,
        true,
        MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings);

    private static EntityId Character(int index) => new($"character:d3/{index:D3}");

    private static EntityId Household(int index) => new($"household:d3/{index:D3}");
}
