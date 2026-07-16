using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionSupportCampaignTests
{
    private static readonly CampaignDate Date = new(200, 8, 1);
    private static readonly EntityId Subject = Character("subject");
    private static readonly EntityId Supporter = Character("supporter");
    private static readonly EntityId FirstCandidate = Character("candidate-a");
    private static readonly EntityId SecondCandidate = Character("candidate-b");

    [Fact]
    public void F803_F806_RegisteredWorkflowDeclaresReplacesAndWithdrawsExactSupport()
    {
        CampaignSimulation simulation = CreateSimulation();
        WorldSnapshot before = simulation.World.CaptureSnapshot();

        CampaignCommand declaration = Declare("declare", FirstCandidate, null);
        Assert.True(simulation.Submit(declaration).IsValid);
        CampaignEvent declaredEvent = Assert.Single(simulation.ResolveTurn());
        CharacterSuccessionSupportActionResolvedEventPayload declaredPayload =
            Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
                declaredEvent.Payload);
        SuccessionSupportState first =
            Assert.IsType<SuccessionSupportDeclaredOutcome>(
                declaredPayload.Outcome).CurrentSupport;
        Assert.Equal(
            CharacterSuccessionIds.DeriveSupportActionEventId(
                Date,
                declaration.CommandId),
            declaredEvent.EventId);
        Assert.Equal(
            new[] { Subject, Supporter, FirstCandidate, first.SupportId }
                .Order(),
            declaredEvent.AffectedIds);
        Assert.NotEqual(
            SimulationChecksum.Compute(before),
            SimulationChecksum.Compute(simulation.World.CaptureSnapshot()));

        CampaignCommand replacement = Declare(
            "replace",
            SecondCandidate,
            first.SupportId,
            simulation.World.Calendar.Date);
        Assert.True(simulation.Submit(replacement).IsValid);
        CharacterSuccessionSupportActionResolvedEventPayload replacementPayload =
            Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
                Assert.Single(simulation.ResolveTurn()).Payload);
        SuccessionSupportReplacedOutcome replaced =
            Assert.IsType<SuccessionSupportReplacedOutcome>(
                replacementPayload.Outcome);
        Assert.Equal(SuccessionSupportStatus.Replaced, replaced.PreviousSupport.Status);
        Assert.Equal(SecondCandidate, replaced.CurrentSupport.SupportedCandidateId);
        Assert.Equal(
            replaced.PreviousSupport.ResolutionEventId,
            replaced.CurrentSupport.SourceEventId);

        CampaignCommand withdrawal = Withdraw(
            "withdraw",
            replaced.CurrentSupport.SupportId,
            simulation.World.Calendar.Date);
        Assert.True(simulation.Submit(withdrawal).IsValid);
        SuccessionSupportWithdrawnOutcome withdrawn =
            Assert.IsType<SuccessionSupportWithdrawnOutcome>(
                Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome);
        Assert.Equal(SuccessionSupportStatus.Withdrawn, withdrawn.PreviousSupport.Status);
        Assert.False(simulation.World.CharacterSuccessions.TryGetCurrentSupport(
            Subject,
            Supporter,
            out _));
        Assert.Equal(
            2,
            simulation.World.CharacterSuccessions
                .GetRecentSupportRecordsForSubject(Subject).Count);
    }

    [Fact]
    public void F807_ConcurrentDeclarationsAreSubmissionOrderInvariant()
    {
        CampaignCommand first = Declare("race-a", FirstCandidate, null);
        CampaignCommand second = Declare("race-b", SecondCandidate, null);

        (string Events, string Snapshot) forward = RunConcurrent(first, second);
        (string Events, string Snapshot) reverse = RunConcurrent(second, first);

        Assert.Equal(forward, reverse);
        CampaignEvent[] events = JsonSerializer.Deserialize<CampaignEvent[]>(
            forward.Events,
            SimulationJson.CreateOptions())!;
        Assert.Single(events, item =>
            item.Payload is CharacterSuccessionSupportActionResolvedEventPayload);
        Assert.Single(events, item => item.Payload is CommandCancelledEventPayload);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("supporter")]
    [InlineData("candidate")]
    public void F808_LaterParticipantDeathDoesNotRewriteSupportEvidence(
        string participant)
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(
            Declare("death-source", FirstCandidate, null)).IsValid);
        SuccessionSupportState original =
            Assert.IsType<SuccessionSupportDeclaredOutcome>(
                Assert.IsType<CharacterSuccessionSupportActionResolvedEventPayload>(
                    Assert.Single(simulation.ResolveTurn()).Payload).Outcome)
                .CurrentSupport;
        EntityId target = participant switch
        {
            "subject" => Subject,
            "supporter" => Supporter,
            _ => FirstCandidate,
        };
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            target,
            out AuthoritativeCharacterProfile? profile));
        CampaignCommand death = CampaignCommand.Create(
            new EntityId($"command:test/f8-death-{participant}"),
            CharacterConditionSystem.AuthoritativeActorId,
            simulation.World.Calendar.Date,
            new CharacterConditionActionCommandPayload(
                new ResolveCharacterDeathAction(target, profile.Condition)));

        Assert.True(simulation.Submit(death).IsValid);
        Assert.IsType<CharacterConditionActionResolvedEventPayload>(
            Assert.Single(simulation.ResolveTurn()).Payload);
        Assert.True(simulation.World.CharacterSuccessions.TryGetCurrentSupport(
            Subject,
            Supporter,
            out SuccessionSupportState? stored));
        Assert.Equal(original, stored);
    }

    [Fact]
    public void F811_PendingSaveReplayAndTamperRejectionAreExact()
    {
        CampaignSimulation pendingSimulation = CreateSimulation();
        CampaignCommand pending = Declare(
            "pending",
            FirstCandidate,
            null,
            Date.AddDays(2));
        Assert.True(pendingSimulation.Submit(pending).IsValid);
        SaveEnvelope envelope = SaveStoreRoundTrip(
            SaveEnvelope.Create("test", [], pendingSimulation));
        CampaignSimulation first = new(WorldState.Restore(envelope.Snapshot));
        CampaignSimulation second = new(WorldState.Restore(envelope.Snapshot));

        CampaignEvent[] firstEvents = first.ResolveTurn().ToArray();
        CampaignEvent[] secondEvents = second.ResolveTurn().ToArray();

        Assert.Equal(Serialize(firstEvents), Serialize(secondEvents));
        Assert.Equal(
            SimulationChecksum.Compute(first.World.CaptureSnapshot()),
            SimulationChecksum.Compute(second.World.CaptureSnapshot()));

        WorldState world = CreateSimulation().World;
        EntityId commandId = new("command:test/f8-tampered");
        EntityId eventId = CharacterSuccessionIds.DeriveSupportActionEventId(
            Date,
            commandId);
        CharacterSuccessionSupportActionResolvedEventPayload planned =
            world.CharacterSuccessions.PlanSupportAction(
                Supporter,
                new(new DeclareSuccessionSupportAction(
                    Subject,
                    FirstCandidate,
                    null)),
                Date,
                world.Calendar.TurnIndex,
                commandId,
                eventId);
        SuccessionSupportState support =
            Assert.IsType<SuccessionSupportDeclaredOutcome>(
                planned.Outcome).CurrentSupport;
        CharacterSuccessionSupportActionResolvedEventPayload tampered = planned with
        {
            Outcome = new SuccessionSupportDeclaredOutcome(support with
            {
                SupportId = new("succession_support:test/tampered"),
            }),
        };
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterSuccessionSupportActionAffectedIds(tampered),
            tampered);
        string before = Serialize(world.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => world.Apply(campaignEvent));
        Assert.Equal(before, Serialize(world.CaptureSnapshot()));
    }

    private static (string Events, string Snapshot) RunConcurrent(
        CampaignCommand first,
        CampaignCommand second)
    {
        CampaignSimulation simulation = CreateSimulation();
        Assert.True(simulation.Submit(first).IsValid);
        Assert.True(simulation.Submit(second).IsValid);
        return (
            Serialize(simulation.ResolveTurn()),
            Serialize(simulation.World.CaptureSnapshot()));
    }

    private static CampaignCommand Declare(
        string suffix,
        EntityId candidateId,
        EntityId? expectedCurrentSupportId,
        CampaignDate? date = null) => CampaignCommand.Create(
        new EntityId($"command:test/f8-{suffix}"),
        Supporter,
        date ?? Date,
        new CharacterSuccessionSupportActionCommandPayload(
            new DeclareSuccessionSupportAction(
                Subject,
                candidateId,
                expectedCurrentSupportId)));

    private static CampaignCommand Withdraw(
        string suffix,
        EntityId expectedCurrentSupportId,
        CampaignDate date) => CampaignCommand.Create(
        new EntityId($"command:test/f8-{suffix}"),
        Supporter,
        date,
        new CharacterSuccessionSupportActionCommandPayload(
            new WithdrawSuccessionSupportAction(
                Subject,
                expectedCurrentSupportId)));

    private static CampaignSimulation CreateSimulation()
    {
        EntityId[] ids = [Subject, Supporter, FirstCandidate, SecondCandidate];
        WorldState world = WorldState.Create(
            Date,
            58,
            [],
            GeographicWorldSnapshot.Empty,
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                ids.Select(Definition).ToArray(),
                [],
                [],
                ids.Select(State).ToArray(),
                [],
                []),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty);
        return new CampaignSimulation(world);
    }

    private static CharacterDefinition Definition(EntityId id)
    {
        EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
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
    }

    private static CharacterState State(EntityId id) => new(
        CharacterContractVersions.State,
        id,
        [],
        [],
        CharacterConditionState.Default,
        []);

    private static SaveEnvelope SaveStoreRoundTrip(SaveEnvelope envelope)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-f8-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "support.save.gz");
            SaveStore store = new();
            store.SaveAtomic(path, envelope);
            return store.Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EntityId Character(string suffix) =>
        new($"character:test/f8-{suffix}");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(
        value,
        CanonicalJson.Options);
}
