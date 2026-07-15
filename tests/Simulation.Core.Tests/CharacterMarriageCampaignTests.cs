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
        return Assert.Single(simulation.ResolveTurn());
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

    private static MarriagePracticeState Practice(int maxPrincipal = 1) => new(
        CharacterMarriageContractVersions.Practice,
        PracticeId,
        18,
        18,
        maxPrincipal,
        4,
        1,
        true,
        true,
        MarriageProhibitedKinship.DirectLine | MarriageProhibitedKinship.Siblings);

    private static EntityId Character(int index) => new($"character:marriage/d1_{index:D4}");
}
