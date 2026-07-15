using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterMarriageCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId PracticeId = new("marriage_practice:test/d1");
    private readonly ITestOutputHelper output;

    public CharacterMarriageCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void D101_ActionOutcomeAndOuterDiscriminatorsAreVersionedAndRegistered()
    {
        CharacterMarriageActionCommandPayload payload = new(
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId));
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:marriage/discriminator"),
            Character(0),
            Date,
            payload);
        string json = JsonSerializer.Serialize(command, SimulationJson.CreateOptions());
        CampaignCommand roundTrip = JsonSerializer.Deserialize<CampaignCommand>(
            json,
            SimulationJson.CreateOptions())!;

        Assert.Equal(1, CharacterMarriageContractVersions.Action);
        Assert.Equal(1, CharacterMarriageContractVersions.Outcome);
        Assert.Equal("character_marriage_action.v1", command.CommandType);
        Assert.Contains("character_marriage_action.v1", json, StringComparison.Ordinal);
        Assert.Contains("propose_political_marriage.v1", json, StringComparison.Ordinal);
        Assert.IsType<ProposePoliticalMarriageAction>(
            Assert.IsType<CharacterMarriageActionCommandPayload>(roundTrip.Payload).Action);

        CampaignSimulation simulation = CreateSimulation(2);
        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        Assert.Equal("character_marriage_action_resolved.v1", campaignEvent.EventType);
        string eventJson = JsonSerializer.Serialize(campaignEvent, SimulationJson.CreateOptions());
        Assert.Contains("character_marriage_action_resolved.v1", eventJson, StringComparison.Ordinal);
        Assert.Contains("marriage_proposal_created.v1", eventJson, StringComparison.Ordinal);
    }

    [Fact]
    public void D102_LengthFramedStableIdsHaveExactGoldens()
    {
        EntityId commandId = new("command:marriage/golden");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(Date, commandId);
        EntityId proposalId = CharacterMarriageIds.DeriveProposalId(
            MarriageProposalKind.PoliticalBetrothal,
            Date,
            commandId);

        Assert.Equal(
            "event:sha256/b9a65636ba12e43a7c9b907e0288114b96bfcd16d37b44bd19bde2249c05998d",
            eventId.Value);
        Assert.Equal(
            "marriage_proposal:sha256/9b81c1f6de0b86cc129407ac0efd7528709261e9e7cf06d3a1c240c877752531",
            proposalId.Value);
        Assert.Equal(
            "political_betrothal:sha256/b43ff9657178feb07570d73632ef49bbbe61ae2eb09320b7b1b0a47eb6890772",
            CharacterMarriageIds.DerivePoliticalBetrothalId(proposalId).Value);
        Assert.Equal(
            "marriage_union:sha256/36e33d9984f4d3e226e0d2b8a025621c00ebb12f6bfafc4f11da8127e734ae0a",
            CharacterMarriageIds.DeriveMarriageUnionId(proposalId).Value);
        Assert.Equal(eventId, CharacterMarriageIds.DeriveActionEventId(Date, commandId));
    }

    [Fact]
    public void D103_DirectPoliticalUnionIsParticipantIssuedAndAtomic()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        CampaignEvent createdEvent = SubmitAndResolve(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "direct/create");
        CharacterMarriageActionResolvedEventPayload created = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(createdEvent.Payload);
        MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            created.Outcome).Proposal;
        Assert.Equal(MarriageBasis.Political, proposal.Basis);
        Assert.Equal(MarriageConsentKind.PoliticalArrangement, proposal.ConsentKind);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);

        CampaignCommand unauthorized = Command(
            simulation,
            Character(2),
            new RespondToPoliticalMarriageProposalAction(
                proposal.ProposalId,
                MarriageProposalResponse.Accept),
            "direct/third-party");
        Assert.False(simulation.Submit(unauthorized).IsValid);

        CampaignEvent acceptedEvent = SubmitAndResolve(
            simulation,
            Character(1),
            new RespondToPoliticalMarriageProposalAction(
                proposal.ProposalId,
                MarriageProposalResponse.Accept),
            "direct/accept");
        CharacterMarriageActionResolvedEventPayload accepted = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(acceptedEvent.Payload);
        DirectPoliticalUnionAcceptedOutcome outcome = Assert.IsType<
            DirectPoliticalUnionAcceptedOutcome>(accepted.Outcome);

        Assert.Equal(MarriageProposalStatus.Accepted, outcome.Proposal.Status);
        Assert.Equal(acceptedEvent.CausalId, outcome.Proposal.ResolutionCommandId);
        Assert.Equal(MarriageBasis.Political, outcome.Union.Basis);
        Assert.Equal(MarriageConsentKind.PoliticalArrangement, outcome.Union.ConsentKind);
        Assert.Equal(outcome.Proposal.ProposalId, outcome.Union.SourceProposalId);
        Assert.Equal(
            CharacterMarriageIds.DeriveMarriageUnionId(outcome.Proposal.ProposalId),
            outcome.Union.UnionId);
        Assert.Equal(outcome.Union, Assert.Single(simulation.World.CharacterMarriages.Unions));
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
        Assert.Equal(
            WorldState.GetCharacterMarriageActionAffectedIds(accepted),
            acceptedEvent.AffectedIds);
    }

    [Fact]
    public void D104_MinorPolicyIsBetrothalOnlyAndEitherParticipantMayCancel()
    {
        CampaignSimulation simulation = CreateSimulation(
            3,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(188, 1, 1),
                [Character(1)] = new CampaignDate(187, 1, 1),
            });
        CampaignCommand legal = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "minor/legal");
        Assert.False(simulation.Submit(legal).IsValid);

        MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(0),
                new ProposePoliticalMarriageAction(
                    Character(1),
                    MarriageProposalKind.PoliticalBetrothal,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "minor/create").Payload).Outcome).Proposal;
        PoliticalBetrothalState betrothal = Assert.IsType<PoliticalBetrothalAcceptedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(1),
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Accept),
                "minor/accept").Payload).Outcome).Betrothal;

        Assert.Equal(PoliticalBetrothalStatus.Active, betrothal.Status);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        PoliticalBetrothalCancelledOutcome cancelled = Assert.IsType<
            PoliticalBetrothalCancelledOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(0),
                    new CancelPoliticalBetrothalAction(betrothal.BetrothalId),
                    "minor/cancel").Payload).Outcome);
        Assert.Equal(PoliticalBetrothalStatus.Cancelled, cancelled.Betrothal.Status);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
    }

    [Fact]
    public void D105_RefusalAndWithdrawalAreTerminalWithoutPartialOutcome()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        MarriageProposalState refusedProposal = CreateProposal(
            simulation,
            Character(0),
            Character(1),
            "terminal/refuse/create");
        MarriageProposalRefusedOutcome refused = Assert.IsType<MarriageProposalRefusedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(1),
                new RespondToPoliticalMarriageProposalAction(
                    refusedProposal.ProposalId,
                    MarriageProposalResponse.Refuse),
                "terminal/refuse").Payload).Outcome);
        Assert.Equal(MarriageProposalStatus.Refused, refused.Proposal.Status);

        MarriageProposalState withdrawnProposal = CreateProposal(
            simulation,
            Character(0),
            Character(2),
            "terminal/withdraw/create");
        MarriageProposalWithdrawnOutcome withdrawn = Assert.IsType<MarriageProposalWithdrawnOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(0),
                new WithdrawPoliticalMarriageProposalAction(withdrawnProposal.ProposalId),
                "terminal/withdraw").Payload).Outcome);
        Assert.Equal(MarriageProposalStatus.Withdrawn, withdrawn.Proposal.Status);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.CharacterMarriages.Betrothals);
    }

    [Theory]
    [InlineData(MarriageBasis.Romantic, MarriageConsentKind.Voluntary)]
    [InlineData(MarriageBasis.Political, MarriageConsentKind.Coerced)]
    public void D105B_RomanticAndCoercedProposalsCannotEnterThePoliticalWorkflow(
        MarriageBasis basis,
        MarriageConsentKind consent)
    {
        CharacterMarriageWorldSnapshot source = ActiveProposalSnapshot();
        MarriageProposalState proposal = Assert.Single(source.Proposals) with
        {
            Basis = basis,
            ConsentKind = consent,
        };
        CampaignSimulation simulation = CreateSimulation(
            2,
            marriageSnapshot: source with { Proposals = [proposal] });
        CampaignCommand response = Command(
            simulation,
            Character(1),
            new RespondToPoliticalMarriageProposalAction(
                proposal.ProposalId,
                MarriageProposalResponse.Accept),
            "political-only/reject");

        Assert.Contains(
            simulation.Submit(response).Issues,
            issue => issue.Code == "proposal_not_political_arrangement");
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
    }

    [Fact]
    public void D106_ConcurrentAcceptanceStalenessCancelsProposalWithoutPartialOutcome()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        MarriageProposalState first = CreateProposal(
            simulation,
            Character(0),
            Character(2),
            "stale/first/create");
        MarriageProposalState second = CreateProposal(
            simulation,
            Character(1),
            Character(2),
            "stale/second/create");
        CampaignCommand acceptFirst = Command(
            simulation,
            Character(2),
            new RespondToPoliticalMarriageProposalAction(
                first.ProposalId,
                MarriageProposalResponse.Accept),
            "stale/first/accept");
        CampaignCommand acceptSecond = Command(
            simulation,
            Character(2),
            new RespondToPoliticalMarriageProposalAction(
                second.ProposalId,
                MarriageProposalResponse.Accept),
            "stale/second/accept");
        Assert.True(simulation.Submit(acceptFirst).IsValid);
        Assert.True(simulation.Submit(acceptSecond).IsValid);

        CharacterMarriageActionResolvedEventPayload[] outcomes = simulation.ResolveTurn()
            .Select(item => Assert.IsType<CharacterMarriageActionResolvedEventPayload>(item.Payload))
            .ToArray();
        Assert.Single(outcomes, item => item.Outcome is DirectPoliticalUnionAcceptedOutcome);
        MarriageProposalCancelledOutcome cancelled = Assert.IsType<MarriageProposalCancelledOutcome>(
            Assert.Single(outcomes, item => item.Outcome is MarriageProposalCancelledOutcome).Outcome);
        Assert.Equal(MarriageProposalStatus.Cancelled, cancelled.Proposal.Status);
        Assert.Single(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.CharacterMarriages.Betrothals);
    }

    [Fact]
    public void D106B_InterveningTerminalTargetProducesGenericCommandCancellation()
    {
        CampaignSimulation simulation = CreateSimulation(2);
        MarriageProposalState proposal = CreateProposal(
            simulation,
            Character(0),
            Character(1),
            "terminal-race/create");
        CampaignCommand accept = Command(
            simulation,
            Character(1),
            new RespondToPoliticalMarriageProposalAction(
                proposal.ProposalId,
                MarriageProposalResponse.Accept),
            "terminal-race/accept");
        CampaignCommand withdraw = Command(
            simulation,
            Character(0),
            new WithdrawPoliticalMarriageProposalAction(proposal.ProposalId),
            "terminal-race/withdraw");
        Assert.True(simulation.Submit(accept).IsValid);
        Assert.True(simulation.Submit(withdraw).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        Assert.Single(events, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.NotEqual(
            MarriageProposalStatus.Active,
            Assert.Single(simulation.World.CharacterMarriages.Proposals).Status);
    }

    [Fact]
    public void D106C_ConcurrentProposalCreationRevalidatesDuplicateAndEightRecipientLimit()
    {
        CampaignSimulation duplicates = CreateSimulation(2);
        CampaignCommand duplicateA = Command(
            duplicates,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "duplicates/a");
        CampaignCommand duplicateB = Command(
            duplicates,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "duplicates/b");
        Assert.True(duplicates.Submit(duplicateA).IsValid);
        Assert.True(duplicates.Submit(duplicateB).IsValid);
        CampaignEvent[] duplicateEvents = duplicates.ResolveTurn().ToArray();
        Assert.Single(duplicateEvents, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(duplicateEvents, item => item.Payload is CommandCancelledEventPayload);
        Assert.Single(duplicates.World.CharacterMarriages.Proposals);

        CampaignSimulation recipientLimit = CreateSimulation(10);
        CampaignCommand[] commands = Enumerable.Range(0, 9)
            .Select(index => Command(
                recipientLimit,
                Character(index),
                new ProposePoliticalMarriageAction(
                    Character(9),
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                $"recipient-limit/{index}"))
            .ToArray();
        Assert.All(commands, command => Assert.True(recipientLimit.Submit(command).IsValid));
        CampaignEvent[] limitedEvents = recipientLimit.ResolveTurn().ToArray();
        Assert.Equal(
            8,
            limitedEvents.Count(item => item.Payload is CharacterMarriageActionResolvedEventPayload));
        Assert.Single(limitedEvents, item => item.Payload is CommandCancelledEventPayload);
        Assert.Equal(8, recipientLimit.World.CharacterMarriages.Proposals.Count);
    }

    [Fact]
    public void D106D_MixedPracticeCapsProduceTypedStaleAcceptanceCancellation()
    {
        EntityId strictPracticeId = new("marriage_practice:test/d1_strict");
        EntityId flexiblePracticeId = new("marriage_practice:test/d1_flexible");
        MarriageProposalState strictProposal = ActiveProposal(
            "strict",
            Character(0),
            Character(1),
            strictPracticeId);
        MarriageProposalState flexibleProposal = ActiveProposal(
            "flexible",
            Character(0),
            Character(2),
            flexiblePracticeId);
        CharacterMarriageWorldSnapshot snapshot = new(
            CharacterMarriageContractVersions.Snapshot,
            [
                Practice() with
                {
                    PracticeId = strictPracticeId,
                    MaximumActivePrincipalSpousesPerCharacter = 1,
                },
                Practice() with
                {
                    PracticeId = flexiblePracticeId,
                    MaximumActivePrincipalSpousesPerCharacter = 2,
                },
            ],
            [strictProposal, flexibleProposal],
            [],
            [],
            [],
            []);
        CampaignSimulation simulation = CreateSimulation(3, marriageSnapshot: snapshot);
        EntityId[] commandIds = new[]
        {
            new EntityId("command:marriage/mixed-practice/a"),
            new EntityId("command:marriage/mixed-practice/b"),
        }.OrderBy(id => CharacterMarriageIds.DeriveActionEventId(Date, id)).ToArray();
        CampaignCommand strictAcceptance = CampaignCommand.Create(
            commandIds[0],
            Character(1),
            Date,
            new CharacterMarriageActionCommandPayload(
                new RespondToPoliticalMarriageProposalAction(
                    strictProposal.ProposalId,
                    MarriageProposalResponse.Accept)));
        CampaignCommand flexibleAcceptance = CampaignCommand.Create(
            commandIds[1],
            Character(2),
            Date,
            new CharacterMarriageActionCommandPayload(
                new RespondToPoliticalMarriageProposalAction(
                    flexibleProposal.ProposalId,
                    MarriageProposalResponse.Accept)));
        Assert.True(simulation.Submit(strictAcceptance).IsValid);
        Assert.True(simulation.Submit(flexibleAcceptance).IsValid);

        CharacterMarriageActionResolvedEventPayload[] events = simulation.ResolveTurn()
            .Select(item => Assert.IsType<CharacterMarriageActionResolvedEventPayload>(item.Payload))
            .ToArray();

        Assert.Single(events, item => item.Outcome is DirectPoliticalUnionAcceptedOutcome);
        MarriageProposalCancelledOutcome cancelled = Assert.IsType<
            MarriageProposalCancelledOutcome>(
                Assert.Single(
                    events,
                    item => item.Outcome is MarriageProposalCancelledOutcome).Outcome);
        Assert.Equal(flexibleProposal.ProposalId, cancelled.Proposal.ProposalId);
        Assert.Equal(MarriageProposalStatus.Cancelled, cancelled.Proposal.Status);
        Assert.Single(simulation.World.CharacterMarriages.Unions);
    }

    [Fact]
    public void D107_FulfillmentCreatesExactSecondProposalUnionAndCommonCausality()
    {
        CampaignSimulation simulation = CreateSimulation(
            2,
            marriageSnapshot: ActiveBetrothalSnapshot());
        PoliticalBetrothalState source = Assert.Single(
            simulation.World.CharacterMarriages.Betrothals);
        CampaignEvent campaignEvent = SubmitAndResolve(
            simulation,
            Character(1),
            new FulfillPoliticalBetrothalAction(source.BetrothalId),
            "fulfillment/success");
        CharacterMarriageActionResolvedEventPayload payload = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(campaignEvent.Payload);
        PoliticalBetrothalFulfilledOutcome outcome = Assert.IsType<
            PoliticalBetrothalFulfilledOutcome>(payload.Outcome);

        Assert.Equal(PoliticalBetrothalStatus.Fulfilled, outcome.Betrothal.Status);
        Assert.Equal(outcome.Union.UnionId, outcome.Betrothal.FulfillmentUnionId);
        Assert.Equal(MarriageProposalKind.LegalUnion, outcome.FulfillmentProposal.Kind);
        Assert.Equal(MarriageProposalStatus.Accepted, outcome.FulfillmentProposal.Status);
        Assert.Equal(campaignEvent.CausalId, outcome.Betrothal.ResolutionCommandId);
        Assert.Equal(campaignEvent.CausalId, outcome.FulfillmentProposal.SourceCommandId);
        Assert.Equal(campaignEvent.CausalId, outcome.FulfillmentProposal.ResolutionCommandId);
        Assert.Equal(outcome.FulfillmentProposal.ProposalId, outcome.Union.SourceProposalId);
        Assert.Equal(campaignEvent.ResolutionDate, outcome.Betrothal.ResolutionDate);
        Assert.Equal(campaignEvent.ResolutionDate, outcome.FulfillmentProposal.CreatedDate);
        Assert.Equal(campaignEvent.ResolutionDate, outcome.Union.StartDate);
        Assert.Equal(2, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Single(simulation.World.CharacterMarriages.Unions);
        Assert.DoesNotContain(
            simulation.World.CharacterMarriages.Betrothals,
            item => item.Status == PoliticalBetrothalStatus.Active);
    }

    [Fact]
    public void D108_TemporarilyIneligibleFulfillmentCancelsCommandAndLeavesBetrothalActive()
    {
        CampaignSimulation simulation = CreateSimulation(
            2,
            marriageSnapshot: ActiveBetrothalSnapshot(),
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(0)] = CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                },
            });
        PoliticalBetrothalState source = Assert.Single(
            simulation.World.CharacterMarriages.Betrothals);
        CampaignCommand command = Command(
            simulation,
            Character(1),
            new FulfillPoliticalBetrothalAction(source.BetrothalId),
            "fulfillment/temporary");
        Assert.True(simulation.Submit(command).IsValid);

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        Assert.IsType<CommandCancelledEventPayload>(campaignEvent.Payload);
        Assert.Equal(
            PoliticalBetrothalStatus.Active,
            Assert.Single(simulation.World.CharacterMarriages.Betrothals).Status);
        Assert.Single(simulation.World.CharacterMarriages.Proposals);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
    }

    [Fact]
    public void D109_TamperedNestedOutcomeAffectedOrCausalDataRollsBack()
    {
        CampaignSimulation simulation = CreateSimulation(
            2,
            marriageSnapshot: ActiveProposalSnapshot());
        WorldState world = simulation.World;
        MarriageProposalState proposal = Assert.Single(world.CharacterMarriages.Proposals);
        EntityId commandId = new("command:marriage/tamper");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(Date, commandId);
        CharacterMarriageActionResolvedEventPayload planned = world.CharacterMarriages.PlanAction(
            Character(1),
            new CharacterMarriageActionCommandPayload(
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Accept)),
            Date,
            world.Calendar.TurnIndex,
            commandId,
            eventId);
        string before = SimulationChecksum.Compute(world.CaptureSnapshot()).Value;
        DirectPoliticalUnionAcceptedOutcome accepted = Assert.IsType<
            DirectPoliticalUnionAcceptedOutcome>(planned.Outcome);
        CharacterMarriageActionResolvedEventPayload tamperedPayload = planned with
        {
            Outcome = accepted with
            {
                Union = accepted.Union with
                {
                    ConsentKind = MarriageConsentKind.Coerced,
                },
            },
        };
        CampaignEvent tampered = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterMarriageActionAffectedIds(tamperedPayload),
            tamperedPayload);
        Assert.Throws<SimulationValidationException>(() => world.Apply(tampered));
        Assert.Equal(before, SimulationChecksum.Compute(world.CaptureSnapshot()).Value);

        CampaignEvent wrongAffected = tampered with
        {
            Payload = planned,
            AffectedIds = [],
        };
        Assert.Throws<SimulationValidationException>(() => world.Apply(wrongAffected));
        Assert.Equal(before, SimulationChecksum.Compute(world.CaptureSnapshot()).Value);

        CampaignEvent wrongCausal = tampered with
        {
            Payload = planned,
            CausalId = new EntityId("command:marriage/wrong"),
            AffectedIds = WorldState.GetCharacterMarriageActionAffectedIds(planned),
        };
        Assert.Throws<SimulationValidationException>(() => world.Apply(wrongCausal));
        Assert.Equal(before, SimulationChecksum.Compute(world.CaptureSnapshot()).Value);
    }

    [Fact]
    public void D109B_BackgroundEventCannotInjectACommandPhaseMarriageMutation()
    {
        CampaignSimulation simulation = CreateSimulation(2);
        EntityId commandId = new("command:marriage/background-injection");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(Date, commandId);
        CharacterMarriageActionResolvedEventPayload planned =
            simulation.World.CharacterMarriages.PlanAction(
                Character(0),
                new CharacterMarriageActionCommandPayload(
                    new ProposePoliticalMarriageAction(
                        Character(1),
                        MarriageProposalKind.LegalUnion,
                        MarriageUnionForm.PrincipalSpouse,
                        null,
                        PracticeId)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);
        CampaignEvent injected = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.BackgroundCommit,
            0,
            WorldState.GetCharacterMarriageActionAffectedIds(planned),
            planned);
        string before = JsonSerializer.Serialize(
            simulation.World.CaptureSnapshot(),
            SimulationJson.CreateOptions());

        Assert.Throws<SimulationValidationException>(
            () => simulation.ResolveTurn([injected]));

        Assert.Equal(
            before,
            JsonSerializer.Serialize(
                simulation.World.CaptureSnapshot(),
                SimulationJson.CreateOptions()));
        Assert.Empty(simulation.World.CharacterMarriages.Proposals);
    }

    [Fact]
    public void D110_TerminalRetentionKeepsNewest64AndFoldsBothParticipants()
    {
        CampaignSimulation simulation = CreateSimulation(66);
        for (int index = 1; index <= 65; index++)
        {
            MarriageProposalState proposal = CreateProposal(
                simulation,
                Character(0),
                Character(index),
                $"retention/{index:D2}/create");
            _ = SubmitAndResolve(
                simulation,
                Character(index),
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Refuse),
                $"retention/{index:D2}/refuse");
        }

        Assert.Equal(64, simulation.World.CharacterMarriages.GetProposalsInvolving(Character(0)).Count);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? actorHistory));
        Assert.Equal(1, actorHistory.FoldedProposalCount);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(1),
            out CharacterMarriageHistoryAggregate? recipientHistory));
        Assert.Equal(1, recipientHistory.FoldedProposalCount);
        Assert.DoesNotContain(
            simulation.World.CharacterMarriages.Proposals,
            item => item.RecipientCharacterId == Character(1));
    }

    [Fact]
    public void D110B_TerminalBetrothalRetentionFoldsCausalPairForBothParticipants()
    {
        CampaignSimulation simulation = CreateSimulation(66);
        for (int index = 1; index <= 65; index++)
        {
            MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(0),
                    new ProposePoliticalMarriageAction(
                        Character(index),
                        MarriageProposalKind.PoliticalBetrothal,
                        MarriageUnionForm.PrincipalSpouse,
                        null,
                        PracticeId),
                    $"betrothal-retention/{index:D2}/create").Payload).Outcome).Proposal;
            PoliticalBetrothalState betrothal = Assert.IsType<PoliticalBetrothalAcceptedOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(index),
                    new RespondToPoliticalMarriageProposalAction(
                        proposal.ProposalId,
                        MarriageProposalResponse.Accept),
                    $"betrothal-retention/{index:D2}/accept").Payload).Outcome).Betrothal;
            _ = SubmitAndResolve(
                simulation,
                Character(index),
                new CancelPoliticalBetrothalAction(betrothal.BetrothalId),
                $"betrothal-retention/{index:D2}/cancel");
        }

        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Equal(64, simulation.World.CharacterMarriages.Betrothals.Count);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? actorHistory));
        Assert.Equal(1, actorHistory.FoldedProposalCount);
        Assert.Equal(1, actorHistory.FoldedBetrothalCount);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(1),
            out CharacterMarriageHistoryAggregate? recipientHistory));
        Assert.Equal(1, recipientHistory.FoldedProposalCount);
        Assert.Equal(1, recipientHistory.FoldedBetrothalCount);
    }

    [Fact]
    public void D110C_MixedRetentionTreatsProposalAndAcceptedOutcomeAsOneCausalGroup()
    {
        CampaignSimulation simulation = CreateSimulation(66);
        MarriageProposalState sourceProposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(0),
                new ProposePoliticalMarriageAction(
                    Character(1),
                    MarriageProposalKind.PoliticalBetrothal,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                "mixed-retention/source/create").Payload).Outcome).Proposal;
        PoliticalBetrothalState sourceBetrothal = Assert.IsType<
            PoliticalBetrothalAcceptedOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(1),
                    new RespondToPoliticalMarriageProposalAction(
                        sourceProposal.ProposalId,
                        MarriageProposalResponse.Accept),
                    "mixed-retention/source/accept").Payload).Outcome).Betrothal;
        _ = SubmitAndResolve(
            simulation,
            Character(1),
            new CancelPoliticalBetrothalAction(sourceBetrothal.BetrothalId),
            "mixed-retention/source/cancel");

        for (int index = 2; index <= 65; index++)
        {
            MarriageProposalState proposal = CreateProposal(
                simulation,
                Character(0),
                Character(index),
                $"mixed-retention/{index:D2}/create");
            _ = SubmitAndResolve(
                simulation,
                Character(index),
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Refuse),
                $"mixed-retention/{index:D2}/refuse");
        }

        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Empty(simulation.World.CharacterMarriages.Betrothals);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? actorHistory));
        Assert.Equal(1, actorHistory.FoldedProposalCount);
        Assert.Equal(1, actorHistory.FoldedBetrothalCount);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(1),
            out CharacterMarriageHistoryAggregate? recipientHistory));
        Assert.Equal(1, recipientHistory.FoldedProposalCount);
        Assert.Equal(1, recipientHistory.FoldedBetrothalCount);
    }

    [Fact]
    public void D110D_FulfillmentChainRemainsDetailedWhileItsUnionIsActive()
    {
        CampaignSimulation simulation = CreateSimulation(
            66,
            marriageSnapshot: ActiveBetrothalSnapshot());
        PoliticalBetrothalState source = Assert.Single(
            simulation.World.CharacterMarriages.Betrothals);
        PoliticalBetrothalFulfilledOutcome fulfilled = Assert.IsType<
            PoliticalBetrothalFulfilledOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(1),
                    new FulfillPoliticalBetrothalAction(source.BetrothalId),
                    "fulfillment-retention/fulfill").Payload).Outcome);

        for (int index = 2; index <= 64; index++)
        {
            MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
                Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(0),
                    new ProposePoliticalMarriageAction(
                        Character(index),
                        MarriageProposalKind.PoliticalBetrothal,
                        MarriageUnionForm.PrincipalSpouse,
                        null,
                        PracticeId),
                    $"fulfillment-retention/{index:D2}/create").Payload).Outcome).Proposal;
            _ = SubmitAndResolve(
                simulation,
                Character(index),
                new RespondToPoliticalMarriageProposalAction(
                    proposal.ProposalId,
                    MarriageProposalResponse.Refuse),
                $"fulfillment-retention/{index:D2}/refuse");
        }

        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Contains(
            simulation.World.CharacterMarriages.Proposals,
            item => item.ProposalId == source.SourceProposalId);
        Assert.Contains(
            simulation.World.CharacterMarriages.Proposals,
            item => item.ProposalId == fulfilled.FulfillmentProposal.ProposalId);
        Assert.Equal(
            fulfilled.Betrothal,
            Assert.Single(simulation.World.CharacterMarriages.Betrothals));
        Assert.Equal(
            fulfilled.Union,
            Assert.Single(simulation.World.CharacterMarriages.Unions));
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? actorHistory));
        Assert.Equal(1, actorHistory.FoldedProposalCount);
        Assert.Equal(0, actorHistory.FoldedBetrothalCount);
        Assert.Equal(0, actorHistory.FoldedUnionCount);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(2),
            out CharacterMarriageHistoryAggregate? oldestRecipientHistory));
        Assert.Equal(1, oldestRecipientHistory.FoldedProposalCount);
    }

    [Fact]
    public void D110E_EndedFulfillmentChainAgesOutAndFoldsAllFourRecordsTogether()
    {
        CampaignSimulation simulation = CreateSimulation(
            65,
            marriageSnapshot: EndedFulfillmentChainAtCapacity());

        _ = SubmitAndResolve(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(64),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "ended-fulfillment-retention/newer-active");

        Assert.Equal(63, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Empty(simulation.World.CharacterMarriages.Betrothals);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? actorHistory));
        Assert.Equal(2, actorHistory.FoldedProposalCount);
        Assert.Equal(1, actorHistory.FoldedBetrothalCount);
        Assert.Equal(1, actorHistory.FoldedUnionCount);
        Assert.Equal(Date.AddDays(-30), actorHistory.EarliestDate);
        Assert.Equal(Date.AddDays(-10), actorHistory.LatestDate);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(1),
            out CharacterMarriageHistoryAggregate? partnerHistory));
        Assert.Equal(2, partnerHistory.FoldedProposalCount);
        Assert.Equal(1, partnerHistory.FoldedBetrothalCount);
        Assert.Equal(1, partnerHistory.FoldedUnionCount);
        Assert.Equal(Date.AddDays(-30), partnerHistory.EarliestDate);
        Assert.Equal(Date.AddDays(-10), partnerHistory.LatestDate);
    }

    [Fact]
    public void D111_ActiveRetentionOverflowRejectsSubmissionWithoutMutation()
    {
        CharacterMarriageWorldSnapshot snapshot = ActiveProposalSnapshot(64);
        CampaignSimulation simulation = CreateSimulation(66, marriageSnapshot: snapshot);
        CampaignCommand command = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(65),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "overflow/active");
        string before = JsonSerializer.Serialize(
            simulation.World.CaptureSnapshot(),
            SimulationJson.CreateOptions());

        CommandValidationResult validation = simulation.Submit(command);

        Assert.Contains(
            validation.Issues,
            issue => issue.Code == "retained_proposal_capacity_reached");
        Assert.Equal(
            before,
            JsonSerializer.Serialize(
                simulation.World.CaptureSnapshot(),
                SimulationJson.CreateOptions()));
        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
    }

    [Fact]
    public void D111B_ConcurrentActiveRetentionOverflowCancelsWithoutAbortingTurn()
    {
        CharacterMarriageWorldSnapshot snapshot = ActiveProposalSnapshot(63);
        CampaignSimulation simulation = CreateSimulation(66, marriageSnapshot: snapshot);
        CampaignCommand first = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(64),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "overflow/race-a");
        CampaignCommand second = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(65),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "overflow/race-b");
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();

        Assert.Single(
            events,
            item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Empty(simulation.World.CaptureSnapshot().PendingCommands);
    }

    [Fact]
    public void D111C_FulfillmentCancelsWhenItsFourRecordCausalGroupCannotFit()
    {
        CharacterMarriageWorldSnapshot source = ActiveBetrothalSnapshot();
        MarriageProposalState[] otherActiveProposals = Enumerable.Range(2, 63)
            .Select(index => ActiveProposal(
                $"fulfillment_capacity_{index:D2}",
                Character(0),
                Character(index),
                PracticeId))
            .ToArray();
        CharacterMarriageWorldSnapshot saturated = source with
        {
            Proposals = source.Proposals.Concat(otherActiveProposals).ToArray(),
        };
        CampaignSimulation simulation = CreateSimulation(65, marriageSnapshot: saturated);
        PoliticalBetrothalState betrothal = Assert.Single(
            simulation.World.CharacterMarriages.Betrothals);
        CampaignCommand fulfillment = Command(
            simulation,
            Character(1),
            new FulfillPoliticalBetrothalAction(betrothal.BetrothalId),
            "fulfillment/capacity");
        Assert.True(simulation.Submit(fulfillment).IsValid);

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        Assert.IsType<CommandCancelledEventPayload>(campaignEvent.Payload);
        Assert.Equal(
            PoliticalBetrothalStatus.Active,
            Assert.Single(simulation.World.CharacterMarriages.Betrothals).Status);
        Assert.Equal(64, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.CaptureSnapshot().PendingCommands);
    }

    [Fact]
    public void D112_SnapshotRestoreChecksumPendingAndDiagnosticsRoundTrip()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        _ = SubmitAndResolve(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(2),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "save/resolved");
        CampaignCommand pending = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "save/pending");
        Assert.True(simulation.Submit(pending).IsValid);
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        WorldState restored = WorldState.Restore(snapshot);

        Assert.Equal(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(restored.CaptureSnapshot()));
        Assert.IsType<CharacterMarriageActionCommandPayload>(
            Assert.Single(restored.CaptureSnapshot().PendingCommands).Payload);

        string path = Path.Combine(Path.GetTempPath(), $"marriage-d1-{Guid.NewGuid():N}.save.gz");
        try
        {
            SaveEnvelope envelope = SaveEnvelope.Create(
                "0.1.0",
                [],
                simulation,
                DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
            new SaveStore().SaveAtomic(path, envelope);
            SaveEnvelope loaded = new SaveStore().Load(path);
            Assert.IsType<CharacterMarriageActionCommandPayload>(
                Assert.Single(loaded.Snapshot.PendingCommands).Payload);
            Assert.IsType<CharacterMarriageActionCommandPayload>(
                Assert.Single(
                    loaded.DiagnosticCommands,
                    item => item.CommandId == pending.CommandId).Payload);
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(
                Assert.Single(loaded.DiagnosticEvents).Payload);

            CampaignSimulation loadedSimulation = new(WorldState.Restore(loaded.Snapshot));
            IReadOnlyList<CampaignEvent> originalEvents = simulation.ResolveTurn();
            IReadOnlyList<CampaignEvent> loadedEvents = loadedSimulation.ResolveTurn();
            Assert.Equal(
                JsonSerializer.Serialize(originalEvents, SimulationJson.CreateOptions()),
                JsonSerializer.Serialize(loadedEvents, SimulationJson.CreateOptions()));
            Assert.Equal(
                SimulationChecksum.Compute(simulation.World.CaptureSnapshot()),
                SimulationChecksum.Compute(loadedSimulation.World.CaptureSnapshot()));
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
    public void D113_ShuffledCommandSubmissionReplaysToTheSameChecksum()
    {
        CampaignCommand[] commands = Enumerable.Range(0, 8)
            .Select(index => CampaignCommand.Create(
                new EntityId($"command:marriage/replay/{index:D2}"),
                Character(index),
                Date,
                new CharacterMarriageActionCommandPayload(
                    new ProposePoliticalMarriageAction(
                        Character(8 + index),
                        MarriageProposalKind.LegalUnion,
                        MarriageUnionForm.PrincipalSpouse,
                        null,
                        PracticeId))))
            .ToArray();
        CampaignSimulation ordered = CreateSimulation(16);
        CampaignSimulation shuffled = CreateSimulation(16);
        Assert.All(commands, command => Assert.True(ordered.Submit(command).IsValid));
        Assert.All(commands.Reverse(), command => Assert.True(shuffled.Submit(command).IsValid));
        IReadOnlyList<CampaignEvent> orderedEvents = ordered.ResolveTurn();
        IReadOnlyList<CampaignEvent> shuffledEvents = shuffled.ResolveTurn();

        Assert.Equal(
            orderedEvents.Select(item => item.EventId),
            shuffledEvents.Select(item => item.EventId));
        Assert.Equal(
            SimulationChecksum.Compute(ordered.World.CaptureSnapshot()),
            SimulationChecksum.Compute(shuffled.World.CaptureSnapshot()));
    }

    [Fact]
    public void D113A_LegalProposalUsesExactBirthdayBoundary()
    {
        IReadOnlyDictionary<EntityId, CampaignDate> exactBirthdays =
            new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(182, 5, 10),
                [Character(1)] = new CampaignDate(182, 5, 10),
            };
        CampaignSimulation exact = CreateSimulation(2, birthDates: exactBirthdays);
        Assert.True(exact.Submit(Command(
            exact,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "birthday/exact")).IsValid);

        IReadOnlyDictionary<EntityId, CampaignDate> underageBirthdays =
            new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(182, 5, 10),
                [Character(1)] = new CampaignDate(182, 5, 11),
            };
        CampaignSimulation underage = CreateSimulation(2, birthDates: underageBirthdays);
        CommandValidationResult invalid = underage.Submit(Command(
            underage,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "birthday/underage"));
        Assert.Contains(
            invalid.Issues,
            issue => issue.Code == "marriage_belowminimumage");
    }

    [Fact]
    public void D113B_UnknownNestedMarriageActionDiscriminatorFailsWithoutChangingSource()
    {
        CampaignSimulation simulation = CreateSimulation(2);
        CampaignCommand pending = Command(
            simulation,
            Character(0),
            new ProposePoliticalMarriageAction(
                Character(1),
                MarriageProposalKind.LegalUnion,
                MarriageUnionForm.PrincipalSpouse,
                null,
                PracticeId),
            "unknown/pending");
        Assert.True(simulation.Submit(pending).IsValid);
        SaveEnvelope envelope = SaveEnvelope.Create(
            "0.1.0",
            [],
            simulation,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        System.Text.Json.Nodes.JsonObject json = JsonSerializer.SerializeToNode(
            envelope,
            SimulationJson.CreateOptions())!.AsObject();
        json["snapshot"]!["pendingCommands"]![0]!["payload"]!["action"]!["$type"] =
            "unknown_marriage_action.v1";
        string path = Path.Combine(Path.GetTempPath(), $"marriage-d1-unknown-{Guid.NewGuid():N}.save.gz");
        try
        {
            using (FileStream file = File.Create(path))
            using (GZipStream gzip = new(file, CompressionLevel.SmallestSize))
            {
                JsonSerializer.Serialize(gzip, json, SimulationJson.CreateOptions());
            }

            byte[] sourceBytes = File.ReadAllBytes(path);
            Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
            Assert.Equal(sourceBytes, File.ReadAllBytes(path));
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
    public void D114_ThousandCharacterBoundedWorkflowFixtureRecordsRawPerformance()
    {
        CampaignSimulation simulation = CreateSimulation(1_000);
        Stopwatch workflow = Stopwatch.StartNew();
        CampaignCommand[] proposals = Enumerable.Range(0, 250)
            .Select(index => CampaignCommand.Create(
                new EntityId($"command:marriage/performance/propose/{index:D3}"),
                Character(index),
                Date,
                new CharacterMarriageActionCommandPayload(
                    new ProposePoliticalMarriageAction(
                        Character(500 + index),
                        MarriageProposalKind.LegalUnion,
                        MarriageUnionForm.PrincipalSpouse,
                        null,
                        PracticeId))))
            .ToArray();
        Assert.All(proposals, command => Assert.True(simulation.Submit(command).IsValid));
        Assert.Equal(250, simulation.ResolveTurn().Count);
        workflow.Stop();

        Stopwatch checksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        string checksumValue = SimulationChecksum.Compute(snapshot).Value;
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, SimulationJson.CreateOptions());
        checksum.Stop();
        byte[] compressed;
        using (MemoryStream stream = new())
        {
            using (GZipStream gzip = new(stream, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(json);
            }

            compressed = stream.ToArray();
        }

        Assert.Equal(1_000, simulation.World.Characters.Profiles.Count);
        Assert.Equal(250, simulation.World.CharacterMarriages.Proposals.Count);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.False(string.IsNullOrWhiteSpace(checksumValue));
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"SP-04D1 raw fixture: characters=1000; proposals=250; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={checksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksumValue}");
    }

    [Fact]
    public void D201_RomanceContractsDiscriminatorsAndStableIdsAreExact()
    {
        EntityId offerCommand = new("command:marriage/romance/golden-offer");
        EntityId acceptanceCommand = new("command:marriage/romance/golden-accept");
        EntityId invitationId = CharacterMarriageIds.DeriveRomanceInvitationId(
            Date,
            offerCommand);
        EntityId routeId = CharacterMarriageIds.DeriveRomanceRouteId(
            invitationId,
            acceptanceCommand);

        Assert.Equal(2, CharacterMarriageContractVersions.Snapshot);
        Assert.Equal(2, CharacterMarriageContractVersions.RomanceRouteState);
        Assert.Equal(2, CharacterMarriageContractVersions.AuthoritativeQuery);
        Assert.Equal(2, CharacterMarriageSystem.Version);
        Assert.Equal(
            "romance_invitation:sha256/92bf37d6a7277b8806b29c301dbde83b27abf3e5837c364971a966ffe8619961",
            invitationId.Value);
        Assert.Equal(
            "romance_route:sha256/6bcd5b92ab1a43fb5a56e445070be71a8b81e455acf68e62596fcb2129aa02cc",
            routeId.Value);

        CampaignCommand command = CampaignCommand.Create(
            offerCommand,
            Character(0),
            Date,
            new CharacterMarriageActionCommandPayload(
                new OfferRomanceRouteAction(Character(1), PracticeId)));
        string json = JsonSerializer.Serialize(command, SimulationJson.CreateOptions());
        CampaignCommand restored = JsonSerializer.Deserialize<CampaignCommand>(
            json,
            SimulationJson.CreateOptions())!;
        Assert.Contains("character_marriage_action.v1", json, StringComparison.Ordinal);
        Assert.Contains("offer_romance_route.v1", json, StringComparison.Ordinal);
        Assert.IsType<OfferRomanceRouteAction>(
            Assert.IsType<CharacterMarriageActionCommandPayload>(restored.Payload).Action);
    }

    [Fact]
    public void D201B_EveryRomanceActionAndOutcomeDiscriminatorRoundTrips()
    {
        RomanceInvitationState invitation = ActiveInvitation(
            "discriminator",
            Character(0),
            Character(1));
        RomanceRouteState active = LegacyRoute(
            "discriminator-active",
            Character(0),
            Character(1),
            2);
        RomanceRouteState completed = active with
        {
            ProgressLevel = 4,
            Status = RomanceRouteStatus.Completed,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = new EntityId(
                "command:marriage/discriminator-completed"),
        };
        RomanceRouteState ended = active with
        {
            Status = RomanceRouteStatus.Ended,
            ResolutionDate = Date,
            ResolutionTurnIndex = 0,
            ResolutionCommandId = new EntityId("command:marriage/discriminator-ended"),
        };
        (ICharacterMarriageAction Action, string Discriminator)[] actions =
        [
            (new OfferRomanceRouteAction(Character(1), PracticeId), "offer_romance_route.v1"),
            (new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Accept), "respond_to_romance_invitation.v1"),
            (new WithdrawRomanceInvitationAction(
                invitation.InvitationId), "withdraw_romance_invitation.v1"),
            (new AdvanceRomanceRouteAction(
                active.RouteId,
                active.ProgressLevel), "advance_romance_route.v1"),
            (new EndRomanceRouteAction(active.RouteId), "end_romance_route.v1"),
        ];
        foreach ((ICharacterMarriageAction action, string discriminator) in actions)
        {
            CharacterMarriageActionCommandPayload payload = new(action);
            string json = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
            CharacterMarriageActionCommandPayload restored = JsonSerializer.Deserialize<
                CharacterMarriageActionCommandPayload>(
                    json,
                    SimulationJson.CreateOptions())!;
            Assert.Equal(action.GetType(), restored.Action.GetType());
            Assert.Contains(discriminator, json, StringComparison.Ordinal);
        }

        (ICharacterMarriageActionOutcome Outcome, string Discriminator)[] outcomes =
        [
            (new RomanceInvitationCreatedOutcome(invitation), "romance_invitation_created.v1"),
            (new RomanceInvitationRefusedOutcome(invitation), "romance_invitation_refused.v1"),
            (new RomanceInvitationWithdrawnOutcome(invitation), "romance_invitation_withdrawn.v1"),
            (new RomanceInvitationCancelledOutcome(invitation), "romance_invitation_cancelled.v1"),
            (new RomanceRouteStartedOutcome(
                invitation.InvitationId,
                active), "romance_route_started.v1"),
            (new RomanceRouteAdvancedOutcome(active), "romance_route_advanced.v1"),
            (new RomanceRouteCompletedOutcome(completed), "romance_route_completed.v1"),
            (new RomanceRouteEndedOutcome(ended), "romance_route_ended.v1"),
        ];
        foreach ((ICharacterMarriageActionOutcome outcome, string discriminator) in outcomes)
        {
            CharacterMarriageActionResolvedEventPayload payload = new(
                Character(0),
                new OfferRomanceRouteAction(Character(1), PracticeId),
                outcome);
            string json = JsonSerializer.Serialize(payload, SimulationJson.CreateOptions());
            CharacterMarriageActionResolvedEventPayload restored = JsonSerializer.Deserialize<
                CharacterMarriageActionResolvedEventPayload>(
                    json,
                    SimulationJson.CreateOptions())!;
            Assert.Equal(outcome.GetType(), restored.Outcome.GetType());
            Assert.Contains(discriminator, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void D202_RecipientAcceptanceStartsOneVersionTwoRouteWithExactEvidence()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        CharacterMarriageActionResolvedEventPayload offered = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(0),
                new OfferRomanceRouteAction(Character(1), PracticeId),
                "romance/start-offer").Payload);
        RomanceInvitationState invitation = Assert.IsType<RomanceInvitationCreatedOutcome>(
            offered.Outcome).Invitation;
        Assert.Equal(invitation, Assert.Single(simulation.World.CharacterMarriages.RomanceInvitations));
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);

        CampaignEvent acceptedEvent = SubmitAndResolve(
            simulation,
            Character(1),
            new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Accept),
            "romance/start-accept");
        CharacterMarriageActionResolvedEventPayload accepted = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(acceptedEvent.Payload);
        RomanceRouteState route = Assert.IsType<RomanceRouteStartedOutcome>(
            accepted.Outcome).Route;

        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Equal(route, Assert.Single(simulation.World.CharacterMarriages.RomanceRoutes));
        Assert.Equal(CharacterMarriageContractVersions.RomanceRouteState, route.ContractVersion);
        Assert.Equal(1, route.ProgressLevel);
        Assert.Equal(RomanceRouteStatus.Active, route.Status);
        Assert.Equal(invitation.InvitationId, route.SourceInvitationId);
        Assert.Equal(invitation.InitiatorCharacterId, route.InvitationInitiatorCharacterId);
        Assert.Equal(invitation.CreatedDate, route.InvitationCreatedDate);
        Assert.Equal(invitation.CreatedTurnIndex, route.InvitationCreatedTurnIndex);
        Assert.Equal(invitation.SourceCommandId, route.InvitationSourceCommandId);
        Assert.Equal(acceptedEvent.CausalId, route.SourceCommandId);
        Assert.Equal(acceptedEvent.CausalId, route.LastPositiveProgressCommandId);
        Assert.Equal(acceptedEvent.ResolutionDate, route.LastPositiveProgressDate);
        Assert.Equal(
            CharacterMarriageIds.DeriveRomanceRouteId(
                invitation.InvitationId,
                acceptedEvent.CausalId!.Value),
            route.RouteId);
        Assert.Empty(simulation.World.CharacterMarriages.Proposals);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.Relationships.CaptureSnapshot().Subjects);
        Assert.Equal(
            WorldState.GetCharacterMarriageActionAffectedIds(accepted),
            acceptedEvent.AffectedIds);
        string json = JsonSerializer.Serialize(acceptedEvent, SimulationJson.CreateOptions());
        Assert.Contains("romance_route_started.v1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void D203_RefusalWithdrawalAndAuthorityRemoveInvitationsWithoutRoutes()
    {
        CampaignSimulation simulation = CreateSimulation(4);
        RomanceInvitationState refused = Offer(simulation, Character(0), Character(1), "romance/refuse-offer");
        Assert.False(simulation.Submit(Command(
            simulation,
            Character(2),
            new RespondToRomanceInvitationAction(
                refused.InvitationId,
                RomanceInvitationResponse.Refuse),
            "romance/refuse-third")).IsValid);
        RomanceInvitationRefusedOutcome refusal = Assert.IsType<RomanceInvitationRefusedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(1),
                new RespondToRomanceInvitationAction(
                    refused.InvitationId,
                    RomanceInvitationResponse.Refuse),
                "romance/refuse-recipient").Payload).Outcome);
        Assert.Equal(refused, refusal.Invitation);

        RomanceInvitationState withdrawn = Offer(
            simulation,
            Character(0),
            Character(2),
            "romance/withdraw-offer");
        Assert.False(simulation.Submit(Command(
            simulation,
            Character(2),
            new WithdrawRomanceInvitationAction(withdrawn.InvitationId),
            "romance/withdraw-recipient")).IsValid);
        RomanceInvitationWithdrawnOutcome withdrawal = Assert.IsType<
            RomanceInvitationWithdrawnOutcome>(Assert.IsType<
                CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                    simulation,
                    Character(0),
                    new WithdrawRomanceInvitationAction(withdrawn.InvitationId),
                    "romance/withdraw-initiator").Payload).Outcome);
        Assert.Equal(withdrawn, withdrawal.Invitation);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
        Assert.Empty(simulation.World.CharacterMarriages.History);
    }

    [Fact]
    public void D204_AdvanceUsesExpectedLevelAndCompletesExactlyAtFour()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        RomanceRouteState route = OfferAndAccept(
            simulation,
            Character(0),
            Character(1),
            "romance/progress");

        for (int expected = 1; expected <= 3; expected++)
        {
            CampaignEvent campaignEvent = SubmitAndResolve(
                simulation,
                expected % 2 == 0 ? Character(0) : Character(1),
                new AdvanceRomanceRouteAction(route.RouteId, expected),
                $"romance/progress-{expected}");
            CharacterMarriageActionResolvedEventPayload payload = Assert.IsType<
                CharacterMarriageActionResolvedEventPayload>(campaignEvent.Payload);
            route = expected == 3
                ? Assert.IsType<RomanceRouteCompletedOutcome>(payload.Outcome).Route
                : Assert.IsType<RomanceRouteAdvancedOutcome>(payload.Outcome).Route;
            Assert.Equal(expected + 1, route.ProgressLevel);
            Assert.Equal(campaignEvent.CausalId, route.LastPositiveProgressCommandId);
        }

        Assert.Equal(RomanceRouteStatus.Completed, route.Status);
        Assert.Equal(4, route.ProgressLevel);
        Assert.Equal(route.ResolutionCommandId, route.LastPositiveProgressCommandId);
        Assert.Empty(simulation.World.CharacterMarriages.Proposals);
        Assert.Empty(simulation.World.CharacterMarriages.Unions);
        Assert.Empty(simulation.World.Relationships.CaptureSnapshot().Subjects);
        Assert.False(simulation.Submit(Command(
            simulation,
            Character(0),
            new AdvanceRomanceRouteAction(route.RouteId, 4),
            "romance/progress-terminal")).IsValid);
    }

    [Fact]
    public void D205_EitherParticipantMayEndButThirdPartyCannot()
    {
        CampaignSimulation first = CreateSimulation(3);
        RomanceRouteState firstRoute = OfferAndAccept(
            first,
            Character(0),
            Character(1),
            "romance/end-first");
        Assert.False(first.Submit(Command(
            first,
            Character(2),
            new EndRomanceRouteAction(firstRoute.RouteId),
            "romance/end-third")).IsValid);
        RomanceRouteEndedOutcome endedByFirst = Assert.IsType<RomanceRouteEndedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                first,
                Character(0),
                new EndRomanceRouteAction(firstRoute.RouteId),
                "romance/end-participant-first").Payload).Outcome);
        Assert.Equal(RomanceRouteStatus.Ended, endedByFirst.Route.Status);

        CampaignSimulation second = CreateSimulation(2);
        RomanceRouteState secondRoute = OfferAndAccept(
            second,
            Character(0),
            Character(1),
            "romance/end-second");
        RomanceRouteEndedOutcome endedBySecond = Assert.IsType<RomanceRouteEndedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                second,
                Character(1),
                new EndRomanceRouteAction(secondRoute.RouteId),
                "romance/end-participant-second").Payload).Outcome);
        Assert.Equal(RomanceRouteStatus.Ended, endedBySecond.Route.Status);
    }

    [Fact]
    public void D206_OfferEnforcesBirthdayLifeCapacityCustodyAndDuplicateState()
    {
        CampaignSimulation minor = CreateSimulation(
            2,
            birthDates: new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(182, 5, 11),
            });
        Assert.False(minor.Submit(Command(
            minor,
            Character(0),
            new OfferRomanceRouteAction(Character(1), PracticeId),
            "romance/minor")).IsValid);

        foreach (CharacterConditionState condition in new[]
                 {
                     CharacterConditionState.Default with { IsIncapacitated = true },
                     CharacterConditionState.Default with
                     {
                         CustodyStatus = CharacterCustodyStatus.Captive,
                        CustodianId = Character(1),
                     },
                     new CharacterConditionState(
                         CharacterVitalStatus.Dead,
                         CharacterHealthStatus.Critical,
                         IsIncapacitated: true,
                         CharacterCustodyStatus.Free,
                         null),
                 })
        {
            CampaignSimulation ineligible = CreateSimulation(
                2,
                conditions: new Dictionary<EntityId, CharacterConditionState>
                {
                    [Character(0)] = condition,
                });
            Assert.False(ineligible.Submit(Command(
                ineligible,
                Character(1),
                new OfferRomanceRouteAction(Character(0), PracticeId),
                $"romance/condition-{condition.GetHashCode()}")).IsValid);
        }

        CampaignSimulation duplicate = CreateSimulation(2);
        _ = Offer(duplicate, Character(0), Character(1), "romance/duplicate-first");
        Assert.False(duplicate.Submit(Command(
            duplicate,
            Character(1),
            new OfferRomanceRouteAction(Character(0), PracticeId),
            "romance/duplicate-second")).IsValid);

        RomanceRouteState existingRoute = LegacyRoute(
            "duplicate-route",
            Character(0),
            Character(1),
            1);
        CampaignSimulation duplicateRoute = CreateSimulation(
            2,
            RomanceSnapshot([existingRoute]));
        Assert.False(duplicateRoute.Submit(Command(
            duplicateRoute,
            Character(1),
            new OfferRomanceRouteAction(Character(0), PracticeId),
            "romance/duplicate-route-offer")).IsValid);
    }

    [Fact]
    public void D207_SameLevelSameTurnRaceFirstWinsAndLaterCancels()
    {
        CampaignSimulation simulation = CreateSimulation(2);
        RomanceRouteState route = OfferAndAccept(
            simulation,
            Character(0),
            Character(1),
            "romance/race");
        CampaignCommand first = Command(
            simulation,
            Character(0),
            new AdvanceRomanceRouteAction(route.RouteId, 1),
            "romance/race-a");
        CampaignCommand second = Command(
            simulation,
            Character(1),
            new AdvanceRomanceRouteAction(route.RouteId, 1),
            "romance/race-b");
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        Assert.Single(events, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Equal(2, Assert.Single(simulation.World.CharacterMarriages.RomanceRoutes).ProgressLevel);
    }

    [Fact]
    public void D208_StaleAcceptanceEligibilityProducesTypedCancellationAndRemovesInvitation()
    {
        RomanceInvitationState invitation = ActiveInvitation(
            "stale",
            Character(0),
            Character(1));
        CampaignSimulation simulation = CreateSimulation(
            2,
            RomanceSnapshot(invitations: [invitation]),
            conditions: new Dictionary<EntityId, CharacterConditionState>
            {
                [Character(0)] = CharacterConditionState.Default with
                {
                    IsIncapacitated = true,
                },
            });
        EntityId commandId = new("command:marriage/romance/stale-accept");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(
            simulation.World.Calendar.Date,
            commandId);
        CharacterMarriageActionResolvedEventPayload planned =
            simulation.World.CharacterMarriages.PlanAction(
                Character(1),
                new CharacterMarriageActionCommandPayload(
                    new RespondToRomanceInvitationAction(
                        invitation.InvitationId,
                        RomanceInvitationResponse.Accept)),
                simulation.World.Calendar.Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);
        Assert.IsType<RomanceInvitationCancelledOutcome>(planned.Outcome);

        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            simulation.World.Calendar.Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterMarriageActionAffectedIds(planned),
            planned);
        simulation.World.Apply(campaignEvent);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
    }

    [Fact]
    public void D209_RouteCapacityRaceCancelsGenericallyAndPreservesInvitation()
    {
        RomanceRouteState[] routes = Enumerable.Range(1, 64)
            .Select(index => LegacyRoute(
                $"capacity-{index:D2}",
                Character(0),
                Character(index),
                1))
            .ToArray();
        RomanceInvitationState invitation = ActiveInvitation(
            "capacity",
            Character(0),
            Character(65));
        CampaignSimulation simulation = CreateSimulation(
            66,
            RomanceSnapshot(routes, [invitation]));
        CampaignCommand command = Command(
            simulation,
            Character(65),
            new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Accept),
            "romance/capacity-accept");
        Assert.True(simulation.Submit(command).IsValid);

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        Assert.IsType<CommandCancelledEventPayload>(campaignEvent.Payload);
        Assert.Equal(invitation, Assert.Single(simulation.World.CharacterMarriages.RomanceInvitations));
        Assert.Equal(64, simulation.World.CharacterMarriages.RomanceRoutes.Count);
    }

    [Theory]
    [InlineData(0, RomanceRouteStatus.Active, 1)]
    [InlineData(1, RomanceRouteStatus.Active, 2)]
    [InlineData(2, RomanceRouteStatus.Active, 3)]
    [InlineData(3, RomanceRouteStatus.Completed, 4)]
    [InlineData(4, RomanceRouteStatus.Completed, 4)]
    public void D210_LegacyVersionOneActiveRouteRemainsActionable(
        int initialProgress,
        RomanceRouteStatus expectedStatus,
        int expectedProgress)
    {
        RomanceRouteState legacy = LegacyRoute(
            $"legacy-{initialProgress}",
            Character(0),
            Character(1),
            initialProgress);
        CampaignSimulation simulation = CreateSimulation(
            2,
            RomanceSnapshot([legacy]));
        CampaignEvent campaignEvent = SubmitAndResolve(
            simulation,
            Character(0),
            new AdvanceRomanceRouteAction(legacy.RouteId, initialProgress),
            $"romance/legacy-{initialProgress}");
        CharacterMarriageActionResolvedEventPayload payload = Assert.IsType<
            CharacterMarriageActionResolvedEventPayload>(campaignEvent.Payload);
        RomanceRouteState result = expectedStatus == RomanceRouteStatus.Completed
            ? Assert.IsType<RomanceRouteCompletedOutcome>(payload.Outcome).Route
            : Assert.IsType<RomanceRouteAdvancedOutcome>(payload.Outcome).Route;
        Assert.Equal(CharacterMarriageContractVersions.State, result.ContractVersion);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedProgress, result.ProgressLevel);
        Assert.Null(result.SourceInvitationId);
        Assert.Null(result.LastPositiveProgressCommandId);
        if (expectedStatus == RomanceRouteStatus.Completed)
        {
            Assert.Equal(campaignEvent.ResolutionDate, result.ResolutionDate);
            Assert.Equal(campaignEvent.CausalId, result.ResolutionCommandId);
            Assert.NotNull(result.ResolutionTurnIndex);
        }
        else
        {
            Assert.Null(result.ResolutionDate);
            Assert.Null(result.ResolutionTurnIndex);
            Assert.Null(result.ResolutionCommandId);
        }
    }

    [Fact]
    public void D211_OfferCapacityRaceCancelsLaterCommandWithoutPartialInvitation()
    {
        RomanceInvitationState[] invitations = Enumerable.Range(1, 63)
            .Select(index => ActiveInvitation(
                $"offer-capacity-{index:D2}",
                Character(0),
                Character(index)))
            .ToArray();
        CampaignSimulation simulation = CreateSimulation(
            66,
            RomanceSnapshot(invitations: invitations));
        CampaignCommand first = Command(
            simulation,
            Character(0),
            new OfferRomanceRouteAction(Character(64), PracticeId),
            "romance/offer-capacity-a");
        CampaignCommand second = Command(
            simulation,
            Character(0),
            new OfferRomanceRouteAction(Character(65), PracticeId),
            "romance/offer-capacity-b");
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        Assert.Single(events, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Equal(64, simulation.World.CharacterMarriages.RomanceInvitations.Count);
        Assert.True(
            simulation.World.CharacterMarriages.RomanceInvitations.Any(
                item => item.RecipientCharacterId == Character(64))
            ^ simulation.World.CharacterMarriages.RomanceInvitations.Any(
                item => item.RecipientCharacterId == Character(65)));
    }

    [Fact]
    public void D212_TamperedOutcomeAndBackgroundPhaseRollBackExactly()
    {
        CampaignSimulation simulation = CreateSimulation(3);
        EntityId commandId = new("command:marriage/romance/tamper");
        EntityId eventId = CharacterMarriageIds.DeriveActionEventId(Date, commandId);
        CharacterMarriageActionResolvedEventPayload planned =
            simulation.World.CharacterMarriages.PlanAction(
                Character(0),
                new CharacterMarriageActionCommandPayload(
                    new OfferRomanceRouteAction(Character(1), PracticeId)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);
        RomanceInvitationCreatedOutcome created = Assert.IsType<
            RomanceInvitationCreatedOutcome>(planned.Outcome);
        CharacterMarriageActionResolvedEventPayload tampered = planned with
        {
            Outcome = new RomanceInvitationCreatedOutcome(created.Invitation with
            {
                RecipientCharacterId = Character(2),
            }),
        };
        string before = SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value;
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.CharacterMarriages.PrevalidateOutcome(
                tampered,
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId));
        Assert.Equal(before, SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value);

        CampaignEvent wrongPhase = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.BackgroundCommit,
            0,
            WorldState.GetCharacterMarriageActionAffectedIds(planned),
            planned);
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(wrongPhase));
        Assert.Equal(before, SimulationChecksum.Compute(simulation.World.CaptureSnapshot()).Value);
    }

    [Fact]
    public void D213_CurrentSaveAndPendingReplayPreserveRomanceIdentity()
    {
        CampaignSimulation original = CreateSimulation(2);
        RomanceInvitationState invitation = Offer(
            original,
            Character(0),
            Character(1),
            "romance/replay-offer");
        CampaignCommand acceptance = Command(
            original,
            Character(1),
            new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Accept),
            "romance/replay-accept");
        Assert.True(original.Submit(acceptance).IsValid);
        SaveEnvelope envelope = SaveEnvelope.Create("test", [], original);
        string path = Path.Combine(
            Path.GetTempPath(),
            $"sp04d2-pending-{Guid.NewGuid():N}.save.gz");
        CampaignSimulation restored;
        try
        {
            new SaveStore().SaveAtomic(path, envelope);
            SaveEnvelope loaded = new SaveStore().Load(path);
            restored = new CampaignSimulation(WorldState.Restore(loaded.Snapshot));
            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal(envelope.Checksum, loaded.Checksum);
        }
        finally
        {
            File.Delete(path);
        }

        CampaignEvent originalEvent = Assert.Single(original.ResolveTurn());
        CampaignEvent restoredEvent = Assert.Single(restored.ResolveTurn());
        Assert.Equal(
            JsonSerializer.Serialize(originalEvent, CanonicalJson.Options),
            JsonSerializer.Serialize(restoredEvent, CanonicalJson.Options));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(restored.World.CaptureSnapshot()));
        RomanceRouteState route = Assert.Single(restored.World.CharacterMarriages.RomanceRoutes);
        Assert.Equal(invitation.InvitationId, route.SourceInvitationId);
        string routePath = Path.Combine(
            Path.GetTempPath(),
            $"sp04d2-route-{Guid.NewGuid():N}.save.gz");
        try
        {
            new SaveStore().SaveAtomic(
                routePath,
                SaveEnvelope.Create("test", [], restored));
            SaveEnvelope routeSave = new SaveStore().Load(routePath);
            Assert.Equal(
                route,
                Assert.Single(routeSave.Snapshot.CharacterMarriages.RomanceRoutes));
        }
        finally
        {
            File.Delete(routePath);
        }
    }

    [Theory]
    [InlineData("offer")]
    [InlineData("accept")]
    [InlineData("refuse")]
    [InlineData("withdraw")]
    [InlineData("advance")]
    [InlineData("end")]
    public void D213B_EveryPendingRomanceActionReplaysByteExactlyAfterSaveLoad(
        string scenario)
    {
        CampaignSimulation original = CreateSimulation(2);
        CampaignCommand pending;
        if (scenario == "offer")
        {
            pending = Command(
                original,
                Character(0),
                new OfferRomanceRouteAction(Character(1), PracticeId),
                "romance/replay-all-offer");
        }
        else if (scenario is "accept" or "refuse" or "withdraw")
        {
            RomanceInvitationState invitation = Offer(
                original,
                Character(0),
                Character(1),
                $"romance/replay-all-{scenario}-setup");
            ICharacterMarriageAction action = scenario switch
            {
                "accept" => new RespondToRomanceInvitationAction(
                    invitation.InvitationId,
                    RomanceInvitationResponse.Accept),
                "refuse" => new RespondToRomanceInvitationAction(
                    invitation.InvitationId,
                    RomanceInvitationResponse.Refuse),
                _ => new WithdrawRomanceInvitationAction(invitation.InvitationId),
            };
            pending = Command(
                original,
                scenario == "withdraw" ? Character(0) : Character(1),
                action,
                $"romance/replay-all-{scenario}");
        }
        else
        {
            RomanceRouteState route = OfferAndAccept(
                original,
                Character(0),
                Character(1),
                $"romance/replay-all-{scenario}-setup");
            pending = Command(
                original,
                Character(0),
                scenario == "advance"
                    ? new AdvanceRomanceRouteAction(route.RouteId, route.ProgressLevel)
                    : new EndRomanceRouteAction(route.RouteId),
                $"romance/replay-all-{scenario}");
        }

        Assert.True(original.Submit(pending).IsValid);
        string path = Path.Combine(
            Path.GetTempPath(),
            $"sp04d2-pending-{scenario}-{Guid.NewGuid():N}.save.gz");
        CampaignSimulation restored;
        try
        {
            SaveEnvelope envelope = SaveEnvelope.Create("test", [], original);
            new SaveStore().SaveAtomic(path, envelope);
            SaveEnvelope loaded = new SaveStore().Load(path);
            restored = new CampaignSimulation(WorldState.Restore(loaded.Snapshot));
            Assert.Equal(envelope.Checksum, loaded.Checksum);
        }
        finally
        {
            File.Delete(path);
        }

        CampaignEvent[] originalEvents = original.ResolveTurn().ToArray();
        CampaignEvent[] restoredEvents = restored.ResolveTurn().ToArray();
        Assert.Equal(
            JsonSerializer.Serialize(originalEvents, CanonicalJson.Options),
            JsonSerializer.Serialize(restoredEvents, CanonicalJson.Options));
        Assert.Equal(
            SimulationChecksum.Compute(original.World.CaptureSnapshot()),
            SimulationChecksum.Compute(restored.World.CaptureSnapshot()));
    }

    [Fact]
    public void D214_ThousandCharacterRomanceWorkflowRecordsRawPerformance()
    {
        CampaignSimulation simulation = CreateSimulation(1_000);
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 0; index < 200; index++)
        {
            EntityId first = Character(index * 2);
            EntityId second = Character(index * 2 + 1);
            RomanceRouteState route = OfferAndAccept(
                simulation,
                first,
                second,
                $"romance/performance-{index:D3}");
            for (int progress = 1; progress <= 3; progress++)
            {
                _ = SubmitAndResolve(
                    simulation,
                    progress % 2 == 0 ? first : second,
                    new AdvanceRomanceRouteAction(route.RouteId, progress),
                    $"romance/performance-{index:D3}-{progress}");
            }
        }

        workflow.Stop();
        Stopwatch checksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        string checksumValue = SimulationChecksum.Compute(snapshot).Value;
        checksum.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, CanonicalJson.Options);
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(200, snapshot.CharacterMarriages.RomanceRoutes.Count);
        Assert.All(
            snapshot.CharacterMarriages.RomanceRoutes,
            item => Assert.Equal(RomanceRouteStatus.Completed, item.Status));
        Assert.Empty(snapshot.CharacterMarriages.Proposals);
        Assert.Empty(snapshot.CharacterMarriages.Unions);
        output.WriteLine(
            $"SP-04D2 raw fixture: characters=1000; completed_routes=200; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={checksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksumValue}");
    }

    [Fact]
    public void D215_IncomingInvitationLimitIsExactlyEight()
    {
        RomanceInvitationState[] invitations = Enumerable.Range(0, 8)
            .Select(index => ActiveInvitation(
                $"incoming-{index}",
                Character(index),
                Character(9)))
            .ToArray();
        CampaignSimulation simulation = CreateSimulation(
            10,
            RomanceSnapshot(invitations: invitations));
        Assert.False(simulation.Submit(Command(
            simulation,
            Character(8),
            new OfferRomanceRouteAction(Character(9), PracticeId),
            "romance/incoming-ninth")).IsValid);

        Assert.Throws<SimulationValidationException>(() => CreateSimulation(
            11,
            RomanceSnapshot(invitations:
            [
                .. invitations,
                ActiveInvitation("incoming-overflow", Character(10), Character(9)),
            ])));
    }

    [Fact]
    public void D216_NewActiveRoutePinsAndFoldsOldestTerminalRouteForBothParticipants()
    {
        RomanceRouteState[] terminalRoutes = Enumerable.Range(1, 64)
            .Select(index => LegacyRoute(
                $"retention-{index:D2}",
                Character(0),
                Character(index),
                3) with
            {
                StartDate = Date.AddDays(-100 - index),
                Status = RomanceRouteStatus.Ended,
                ResolutionDate = Date.AddDays(-index),
                ResolutionTurnIndex = 0,
                ResolutionCommandId = new EntityId(
                    $"command:marriage/fixture-route-retention-{index:D2}-end"),
            })
            .ToArray();
        RomanceInvitationState invitation = ActiveInvitation(
            "retention-new",
            Character(0),
            Character(65));
        CampaignSimulation simulation = CreateSimulation(
            66,
            RomanceSnapshot(terminalRoutes, [invitation]));

        _ = SubmitAndResolve(
            simulation,
            Character(65),
            new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Accept),
            "romance/retention-accept");

        Assert.Equal(64, simulation.World.CharacterMarriages.RomanceRoutes.Count);
        Assert.Contains(
            simulation.World.CharacterMarriages.RomanceRoutes,
            route => route.Status == RomanceRouteStatus.Active
                && route.SourceInvitationId == invitation.InvitationId);
        Assert.DoesNotContain(
            simulation.World.CharacterMarriages.RomanceRoutes,
            route => route.RouteId == terminalRoutes[^1].RouteId);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(0),
            out CharacterMarriageHistoryAggregate? firstHistory));
        Assert.Equal(1, firstHistory.FoldedRomanceRouteCount);
        Assert.True(simulation.World.CharacterMarriages.TryGetHistory(
            Character(64),
            out CharacterMarriageHistoryAggregate? secondHistory));
        Assert.Equal(1, secondHistory.FoldedRomanceRouteCount);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void D217_LegacyAdvanceRejectsRetainedCoerciveSourceAndResolutionCommands(
        int progressLevel,
        bool useResolutionCommand)
    {
        CharacterMarriageWorldSnapshot snapshot = CoerciveLegacyRouteSnapshot(
            progressLevel,
            out EntityId sourceCommandId,
            out EntityId resolutionCommandId);
        CampaignSimulation simulation = CreateSimulation(2, snapshot);
        RomanceRouteState route = Assert.Single(
            simulation.World.CharacterMarriages.RomanceRoutes);
        EntityId reusedCommandId = useResolutionCommand
            ? resolutionCommandId
            : sourceCommandId;
        CampaignCommand command = CampaignCommand.Create(
            reusedCommandId,
            Character(0),
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(
                new AdvanceRomanceRouteAction(route.RouteId, progressLevel)));

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent cancelled = Assert.Single(simulation.ResolveTurn());

        Assert.IsType<CommandCancelledEventPayload>(cancelled.Payload);
        RomanceRouteState unchanged = Assert.Single(
            simulation.World.CharacterMarriages.RomanceRoutes);
        Assert.Equal(progressLevel, unchanged.ProgressLevel);
        Assert.Equal(RomanceRouteStatus.Active, unchanged.Status);
    }

    [Fact]
    public void D218_SamePairOfferRaceHasOneStableWinnerAndNoPartialState()
    {
        CampaignSimulation simulation = CreateSimulation(2);
        CampaignCommand first = Command(
            simulation,
            Character(0),
            new OfferRomanceRouteAction(Character(1), PracticeId),
            "romance/same-pair-offer/a");
        CampaignCommand second = Command(
            simulation,
            Character(1),
            new OfferRomanceRouteAction(Character(0), PracticeId),
            "romance/same-pair-offer/b");
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();

        Assert.Single(events, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Single(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceRoutes);
    }

    [Theory]
    [InlineData("refuse", true)]
    [InlineData("refuse", false)]
    [InlineData("withdraw", true)]
    [InlineData("withdraw", false)]
    public void D219_AcceptResponseRacesResolveByStableCommandOrder(
        string competingAction,
        bool acceptFirst)
    {
        CampaignSimulation simulation = CreateSimulation(2);
        string order = acceptFirst ? "accept-first" : "conflict-first";
        RomanceInvitationState invitation = Offer(
            simulation,
            Character(0),
            Character(1),
            $"romance/response-race-{competingAction}-setup-{order}");
        (EntityId earlierId, EntityId laterId) = OrderByMarriageEventId(
            simulation.World.Calendar.Date,
            new EntityId($"command:marriage/romance/response-race-{competingAction}-{order}/one"),
            new EntityId($"command:marriage/romance/response-race-{competingAction}-{order}/two"));
        CampaignCommand accept = CampaignCommand.Create(
            acceptFirst ? earlierId : laterId,
            Character(1),
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(
                new RespondToRomanceInvitationAction(
                    invitation.InvitationId,
                    RomanceInvitationResponse.Accept)));
        ICharacterMarriageAction conflictAction = competingAction == "refuse"
            ? new RespondToRomanceInvitationAction(
                invitation.InvitationId,
                RomanceInvitationResponse.Refuse)
            : new WithdrawRomanceInvitationAction(invitation.InvitationId);
        CampaignCommand conflict = CampaignCommand.Create(
            acceptFirst ? laterId : earlierId,
            competingAction == "refuse" ? Character(1) : Character(0),
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(conflictAction));
        Assert.True(simulation.Submit(accept).IsValid);
        Assert.True(simulation.Submit(conflict).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        bool acceptanceResolvedFirst = CharacterMarriageIds.DeriveActionEventId(
            accept.IssuedDate,
            accept.CommandId).CompareTo(CharacterMarriageIds.DeriveActionEventId(
                conflict.IssuedDate,
                conflict.CommandId)) < 0;
        Assert.Equal(acceptFirst, acceptanceResolvedFirst);

        Assert.Single(events, item => item.Payload is CharacterMarriageActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        Assert.Empty(simulation.World.CharacterMarriages.RomanceInvitations);
        Assert.Equal(
            acceptanceResolvedFirst ? 1 : 0,
            simulation.World.CharacterMarriages.RomanceRoutes.Count);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public void D220_AdvanceAndEndRacesResolveByStableCommandOrder(
        int initialProgress,
        bool endFirst)
    {
        CampaignSimulation simulation = CreateSimulation(2);
        string order = endFirst ? "end-first" : "advance-first";
        RomanceRouteState route = OfferAndAccept(
            simulation,
            Character(0),
            Character(1),
            $"romance/advance-end-{initialProgress}-{order}");
        for (int expected = 1; expected < initialProgress; expected++)
        {
            _ = SubmitAndResolve(
                simulation,
                Character(0),
                new AdvanceRomanceRouteAction(route.RouteId, expected),
                $"romance/advance-end-setup-{initialProgress}-{expected}-{order}");
        }

        (EntityId earlierId, EntityId laterId) = OrderByMarriageEventId(
            simulation.World.Calendar.Date,
            new EntityId($"command:marriage/romance/advance-end-{initialProgress}-{order}/one"),
            new EntityId($"command:marriage/romance/advance-end-{initialProgress}-{order}/two"));
        CampaignCommand advance = CampaignCommand.Create(
            endFirst ? laterId : earlierId,
            Character(0),
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(
                new AdvanceRomanceRouteAction(route.RouteId, initialProgress)));
        CampaignCommand end = CampaignCommand.Create(
            endFirst ? earlierId : laterId,
            Character(0),
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(
                new EndRomanceRouteAction(route.RouteId)));
        Assert.True(simulation.Submit(advance).IsValid);
        Assert.True(simulation.Submit(end).IsValid);

        CampaignEvent[] events = simulation.ResolveTurn().ToArray();
        RomanceRouteState resolved = Assert.Single(
            simulation.World.CharacterMarriages.RomanceRoutes);
        bool endResolvedFirst = CharacterMarriageIds.DeriveActionEventId(
            end.IssuedDate,
            end.CommandId).CompareTo(CharacterMarriageIds.DeriveActionEventId(
                advance.IssuedDate,
                advance.CommandId)) < 0;
        Assert.Equal(endFirst, endResolvedFirst);

        if (endResolvedFirst)
        {
            Assert.Equal(RomanceRouteStatus.Ended, resolved.Status);
            Assert.Equal(initialProgress, resolved.ProgressLevel);
            Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        }
        else if (initialProgress == 1)
        {
            Assert.Equal(RomanceRouteStatus.Ended, resolved.Status);
            Assert.Equal(2, resolved.ProgressLevel);
            Assert.All(events, item => Assert.IsType<CharacterMarriageActionResolvedEventPayload>(
                item.Payload));
        }
        else
        {
            Assert.Equal(RomanceRouteStatus.Completed, resolved.Status);
            Assert.Equal(4, resolved.ProgressLevel);
            Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
        }
    }

    [Theory]
    [InlineData(MarriageProposalKind.LegalUnion)]
    [InlineData(MarriageProposalKind.PoliticalBetrothal)]
    public void D221_PoliticalLegalStateCoexistsWithIndependentRomance(
        MarriageProposalKind proposalKind)
    {
        CampaignSimulation simulation = CreateSimulation(2);
        string kindLabel = proposalKind == MarriageProposalKind.LegalUnion
            ? "legal-union"
            : "political-betrothal";
        MarriageProposalState proposal = Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                Character(0),
                new ProposePoliticalMarriageAction(
                    Character(1),
                    proposalKind,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                $"romance/coexist-{kindLabel}-propose").Payload).Outcome).Proposal;
        _ = SubmitAndResolve(
            simulation,
            Character(1),
            new RespondToPoliticalMarriageProposalAction(
                proposal.ProposalId,
                MarriageProposalResponse.Accept),
            $"romance/coexist-{kindLabel}-accept");

        RomanceRouteState route = OfferAndAccept(
            simulation,
            Character(0),
            Character(1),
            $"romance/coexist-{kindLabel}-romance");

        Assert.Equal(RomanceRouteStatus.Active, route.Status);
        Assert.Equal(
            proposalKind == MarriageProposalKind.LegalUnion ? 1 : 0,
            simulation.World.CharacterMarriages.Unions.Count);
        Assert.Equal(
            proposalKind == MarriageProposalKind.PoliticalBetrothal ? 1 : 0,
            simulation.World.CharacterMarriages.Betrothals.Count);
        Assert.Empty(simulation.World.Relationships.CaptureSnapshot().Subjects);
        Assert.All(
            simulation.World.Characters.Profiles,
            profile => Assert.Null(profile.HouseholdId));
    }

    [Fact]
    public void D222_RomanceWorkflowHonorsExactBirthdayConfiguredMinimumAndPracticeIdentity()
    {
        IReadOnlyDictionary<EntityId, CampaignDate> exactAdultBirthDates =
            new Dictionary<EntityId, CampaignDate>
            {
                [Character(0)] = new CampaignDate(182, 5, 10),
                [Character(1)] = new CampaignDate(182, 5, 10),
            };
        CampaignSimulation exactAdult = CreateSimulation(
            2,
            birthDates: exactAdultBirthDates);
        RomanceRouteState route = OfferAndAccept(
            exactAdult,
            Character(0),
            Character(1),
            "romance/exact-eighteen");
        Assert.Equal(1, route.ProgressLevel);

        CharacterMarriageWorldSnapshot ageNineteenPractice = new(
            CharacterMarriageContractVersions.Snapshot,
            [Practice(minimumRomanceAge: 19)],
            [],
            [],
            [],
            [],
            []);
        CampaignSimulation belowConfiguredMinimum = CreateSimulation(
            2,
            ageNineteenPractice,
            exactAdultBirthDates);
        Assert.False(belowConfiguredMinimum.Submit(Command(
            belowConfiguredMinimum,
            Character(0),
            new OfferRomanceRouteAction(Character(1), PracticeId),
            "romance/configured-minimum")).IsValid);

        CampaignSimulation unknownPractice = CreateSimulation(2);
        Assert.False(unknownPractice.Submit(Command(
            unknownPractice,
            Character(0),
            new OfferRomanceRouteAction(
                Character(1),
                new EntityId("marriage_practice:test/unknown")),
            "romance/unknown-practice")).IsValid);
    }

    private static CampaignEvent SubmitAndResolve(
        CampaignSimulation simulation,
        EntityId actor,
        ICharacterMarriageAction action,
        string commandSuffix)
    {
        CampaignCommand command = Command(simulation, actor, action, commandSuffix);
        CommandValidationResult validation = simulation.Submit(command);
        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        return Assert.Single(
            simulation.ResolveTurn(),
            campaignEvent => campaignEvent.Payload is CharacterMarriageActionResolvedEventPayload);
    }

    private static CampaignCommand Command(
        CampaignSimulation simulation,
        EntityId actor,
        ICharacterMarriageAction action,
        string suffix) => CampaignCommand.Create(
            new EntityId($"command:marriage/{suffix}"),
            actor,
            simulation.World.Calendar.Date,
            new CharacterMarriageActionCommandPayload(action));

    private static RomanceInvitationState Offer(
        CampaignSimulation simulation,
        EntityId initiator,
        EntityId recipient,
        string suffix) => Assert.IsType<RomanceInvitationCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                initiator,
                new OfferRomanceRouteAction(recipient, PracticeId),
                suffix).Payload).Outcome).Invitation;

    private static RomanceRouteState OfferAndAccept(
        CampaignSimulation simulation,
        EntityId initiator,
        EntityId recipient,
        string suffix)
    {
        RomanceInvitationState invitation = Offer(
            simulation,
            initiator,
            recipient,
            $"{suffix}-offer");
        return Assert.IsType<RomanceRouteStartedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                recipient,
                new RespondToRomanceInvitationAction(
                    invitation.InvitationId,
                    RomanceInvitationResponse.Accept),
                $"{suffix}-accept").Payload).Outcome).Route;
    }

    private static RomanceInvitationState ActiveInvitation(
        string suffix,
        EntityId initiator,
        EntityId recipient)
    {
        EntityId commandId = new($"command:marriage/fixture-invitation-{suffix}");
        return new RomanceInvitationState(
            CharacterMarriageContractVersions.RomanceInvitationState,
            CharacterMarriageIds.DeriveRomanceInvitationId(Date, commandId),
            initiator,
            recipient,
            PracticeId,
            Date,
            0,
            commandId);
    }

    private static RomanceRouteState LegacyRoute(
        string suffix,
        EntityId first,
        EntityId second,
        int progress) => new(
            CharacterMarriageContractVersions.State,
            new EntityId($"romance_route:fixture/{suffix}"),
            first.CompareTo(second) < 0 ? first : second,
            first.CompareTo(second) < 0 ? second : first,
            PracticeId,
            progress,
            Date,
            0,
            new EntityId($"command:marriage/fixture-route-{suffix}"),
            RomanceRouteStatus.Active,
            null,
            null,
            null);

    private static CharacterMarriageWorldSnapshot RomanceSnapshot(
        IReadOnlyList<RomanceRouteState>? routes = null,
        IReadOnlyList<RomanceInvitationState>? invitations = null) => new(
            CharacterMarriageContractVersions.Snapshot,
            [Practice(maxPrincipal: 8)],
            [],
            [],
            [],
            routes ?? [],
            [],
            invitations ?? []);

    private static CharacterMarriageWorldSnapshot CoerciveLegacyRouteSnapshot(
        int progressLevel,
        out EntityId sourceCommandId,
        out EntityId resolutionCommandId)
    {
        sourceCommandId = new EntityId("command:marriage/coercive-retained/source");
        resolutionCommandId = new EntityId("command:marriage/coercive-retained/resolution");
        EntityId proposalId = new("marriage_proposal:test/coercive-retained");
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            proposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.Coerced,
            Character(0),
            Character(1),
            null,
            PracticeId,
            Date.AddDays(-2),
            0,
            sourceCommandId,
            MarriageProposalStatus.Accepted,
            Date.AddDays(-1),
            0,
            resolutionCommandId);
        MarriageUnionState union = new(
            CharacterMarriageContractVersions.State,
            new EntityId("marriage_union:test/coercive-retained"),
            Character(0),
            Character(1),
            MarriageUnionForm.PrincipalSpouse,
            null,
            MarriageBasis.Political,
            MarriageConsentKind.Coerced,
            PracticeId,
            proposalId,
            Date.AddDays(-1),
            0,
            MarriageUnionStatus.Active,
            null,
            null,
            null,
            null);
        RomanceRouteState route = LegacyRoute(
            $"coercive-retained-{progressLevel}",
            Character(0),
            Character(1),
            progressLevel) with
        {
            StartDate = Date.AddDays(-3),
        };
        return new CharacterMarriageWorldSnapshot(
            CharacterMarriageContractVersions.Snapshot,
            [Practice()],
            [proposal],
            [],
            [union],
            [route],
            []);
    }

    private static MarriageProposalState CreateProposal(
        CampaignSimulation simulation,
        EntityId proposer,
        EntityId recipient,
        string suffix) => Assert.IsType<MarriageProposalCreatedOutcome>(
            Assert.IsType<CharacterMarriageActionResolvedEventPayload>(SubmitAndResolve(
                simulation,
                proposer,
                new ProposePoliticalMarriageAction(
                    recipient,
                    MarriageProposalKind.LegalUnion,
                    MarriageUnionForm.PrincipalSpouse,
                    null,
                    PracticeId),
                suffix).Payload).Outcome).Proposal;

    private static CampaignSimulation CreateSimulation(
        int characterCount,
        CharacterMarriageWorldSnapshot? marriageSnapshot = null,
        IReadOnlyDictionary<EntityId, CampaignDate>? birthDates = null,
        IReadOnlyDictionary<EntityId, CharacterConditionState>? conditions = null)
    {
        CharacterDefinition[] definitions = Enumerable.Range(0, characterCount)
            .Select(index =>
            {
                EntityId id = Character(index);
                EntityId nameKey = new($"loc:marriage/d1_{index}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    birthDates is not null && birthDates.TryGetValue(id, out CampaignDate birthDate)
                        ? birthDate
                        : new CampaignDate(170, 1, 1),
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
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
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
            []);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260715,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            marriageSnapshot ?? new CharacterMarriageWorldSnapshot(
                CharacterMarriageContractVersions.Snapshot,
                [Practice()],
                [],
                [],
                [],
                [],
                [])));
    }

    private static CharacterMarriageWorldSnapshot ActiveProposalSnapshot(int count = 1)
    {
        MarriageProposalState[] proposals = Enumerable.Range(1, count)
            .Select(index => ActiveProposal(
                $"active_{index:D2}",
                Character(0),
                Character(index),
                PracticeId))
            .ToArray();
        return new CharacterMarriageWorldSnapshot(
            CharacterMarriageContractVersions.Snapshot,
            [Practice(maxPrincipal: 8)],
            proposals,
            [],
            [],
            [],
            []);
    }

    private static MarriageProposalState ActiveProposal(
        string suffix,
        EntityId proposer,
        EntityId recipient,
        EntityId practiceId) => new(
            CharacterMarriageContractVersions.State,
            new EntityId($"marriage_proposal:test/{suffix}"),
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            proposer,
            recipient,
            null,
            practiceId,
            Date,
            0,
            new EntityId($"command:marriage/{suffix}/create"),
            MarriageProposalStatus.Active,
            null,
            null,
            null);

    private static CharacterMarriageWorldSnapshot ActiveBetrothalSnapshot()
    {
        EntityId sourceCommandId = new("command:marriage/source/create");
        EntityId resolutionCommandId = new("command:marriage/source/accept");
        CampaignDate start = Date.AddDays(-10);
        MarriageProposalState proposal = new(
            CharacterMarriageContractVersions.State,
            new EntityId("marriage_proposal:test/source"),
            MarriageProposalKind.PoliticalBetrothal,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            Character(0),
            Character(1),
            null,
            PracticeId,
            start.AddDays(-1),
            0,
            sourceCommandId,
            MarriageProposalStatus.Accepted,
            start,
            0,
            resolutionCommandId);
        PoliticalBetrothalState betrothal = new(
            CharacterMarriageContractVersions.State,
            new EntityId("political_betrothal:test/source"),
            Character(0),
            Character(1),
            MarriageUnionForm.PrincipalSpouse,
            null,
            PracticeId,
            proposal.ProposalId,
            start,
            0,
            PoliticalBetrothalStatus.Active,
            null,
            null,
            null,
            null);
        return new CharacterMarriageWorldSnapshot(
            CharacterMarriageContractVersions.Snapshot,
            [Practice()],
            [proposal],
            [betrothal],
            [],
            [],
            []);
    }

    private static CharacterMarriageWorldSnapshot EndedFulfillmentChainAtCapacity()
    {
        EntityId politicalProposalId = new("marriage_proposal:test/ended_fulfillment_source");
        EntityId betrothalId = new("political_betrothal:test/ended_fulfillment_source");
        EntityId legalProposalId = new("marriage_proposal:test/ended_fulfillment_legal");
        EntityId unionId = new("marriage_union:test/ended_fulfillment");
        EntityId creationCommandId = new("command:marriage/ended_fulfillment/create");
        EntityId acceptanceCommandId = new("command:marriage/ended_fulfillment/accept");
        EntityId fulfillmentCommandId = new("command:marriage/ended_fulfillment/fulfill");
        MarriageProposalState politicalProposal = new(
            CharacterMarriageContractVersions.State,
            politicalProposalId,
            MarriageProposalKind.PoliticalBetrothal,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            Character(0),
            Character(1),
            null,
            PracticeId,
            Date.AddDays(-30),
            0,
            creationCommandId,
            MarriageProposalStatus.Accepted,
            Date.AddDays(-25),
            0,
            acceptanceCommandId);
        MarriageProposalState legalProposal = new(
            CharacterMarriageContractVersions.State,
            legalProposalId,
            MarriageProposalKind.LegalUnion,
            MarriageBasis.Political,
            MarriageUnionForm.PrincipalSpouse,
            MarriageConsentKind.PoliticalArrangement,
            Character(0),
            Character(1),
            null,
            PracticeId,
            Date.AddDays(-20),
            0,
            fulfillmentCommandId,
            MarriageProposalStatus.Accepted,
            Date.AddDays(-20),
            0,
            fulfillmentCommandId);
        PoliticalBetrothalState betrothal = new(
            CharacterMarriageContractVersions.State,
            betrothalId,
            Character(0),
            Character(1),
            MarriageUnionForm.PrincipalSpouse,
            null,
            PracticeId,
            politicalProposalId,
            Date.AddDays(-25),
            0,
            PoliticalBetrothalStatus.Fulfilled,
            unionId,
            Date.AddDays(-20),
            0,
            fulfillmentCommandId);
        MarriageUnionState union = new(
            CharacterMarriageContractVersions.State,
            unionId,
            Character(0),
            Character(1),
            MarriageUnionForm.PrincipalSpouse,
            null,
            MarriageBasis.Political,
            MarriageConsentKind.PoliticalArrangement,
            PracticeId,
            legalProposalId,
            Date.AddDays(-20),
            0,
            MarriageUnionStatus.Ended,
            Date.AddDays(-10),
            0,
            new EntityId("command:marriage/ended_fulfillment/end"),
            MarriageUnionEndReason.PoliticalDissolution);
        MarriageProposalState[] newerRefusals = Enumerable.Range(2, 62)
            .Select(index => new MarriageProposalState(
                CharacterMarriageContractVersions.State,
                new EntityId($"marriage_proposal:test/newer_refusal_{index:D2}"),
                MarriageProposalKind.LegalUnion,
                MarriageBasis.Political,
                MarriageUnionForm.PrincipalSpouse,
                MarriageConsentKind.PoliticalArrangement,
                Character(0),
                Character(index),
                null,
                PracticeId,
                Date.AddDays(-3),
                0,
                new EntityId($"command:marriage/newer_refusal_{index:D2}/create"),
                MarriageProposalStatus.Refused,
                Date.AddDays(-2),
                0,
                new EntityId($"command:marriage/newer_refusal_{index:D2}/refuse")))
            .ToArray();
        return new CharacterMarriageWorldSnapshot(
            CharacterMarriageContractVersions.Snapshot,
            [Practice()],
            new[] { politicalProposal, legalProposal }.Concat(newerRefusals).ToArray(),
            [betrothal],
            [union],
            [],
            []);
    }

    private static MarriagePracticeState Practice(
        int maxPrincipal = 1,
        int minimumRomanceAge = 18) => new(
        CharacterMarriageContractVersions.Practice,
        PracticeId,
        18,
        minimumRomanceAge,
        maxPrincipal,
        4,
        1,
        true,
        true,
        MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings);

    private static (EntityId Earlier, EntityId Later) OrderByMarriageEventId(
        CampaignDate date,
        EntityId first,
        EntityId second) => CharacterMarriageIds.DeriveActionEventId(date, first)
            .CompareTo(CharacterMarriageIds.DeriveActionEventId(date, second)) < 0
                ? (first, second)
                : (second, first);

    private static EntityId Character(int index) => new($"character:marriage/d1_{index:D4}");
}
