using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterGuardianshipCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Guardian = new("character:test/guardian");
    private static readonly EntityId OtherGuardian = new("character:test/other_guardian");
    private static readonly EntityId Ward = new("character:test/ward");
    private static readonly EntityId OtherWard = new("character:test/other_ward");
    private readonly ITestOutputHelper output;

    public CharacterGuardianshipCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void CampaignGuardianship_EnforcesAuthorityAndPhaseAndMutatesOnlyGuardianships()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        Assert.False(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-unauthorized"),
            Guardian,
            Ward,
            issuingActor: Guardian)).IsValid);
        Assert.False(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-wrong-phase"),
            Guardian,
            Ward,
            phase: ResolutionPhase.Systems)).IsValid);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-success"),
            Guardian,
            Ward);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        PrimaryGuardianshipEstablishedOutcome outcome = Assert.IsType<
            PrimaryGuardianshipEstablishedOutcome>(payload.Outcome);
        CharacterGuardianshipState guardianship = outcome.Guardianship;

        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(
            CharacterGuardianshipIds.DeriveGuardianshipId(
                campaignEvent.EventId,
                Ward,
                Guardian),
            guardianship.GuardianshipId);
        Assert.Equal(
            new EntityId[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                guardianship.GuardianshipId,
                Guardian,
                Ward,
            }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(
                Ward,
                out CharacterGuardianshipState? active));
        Assert.Equal(guardianship, active);

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.Equal(Serialize(before.Characters), Serialize(after.Characters));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
    }

    [Fact]
    public void CampaignGuardianship_TamperedOutcomeRollsBackAtomically()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EntityId commandId = new("command:test/guardianship-tampered");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(
                new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        PrimaryGuardianshipEstablishedOutcome outcome = Assert.IsType<
            PrimaryGuardianshipEstablishedOutcome>(plan.ResolvedPayload.Outcome);
        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                Guardianship = outcome.Guardianship with
                {
                    Status = CharacterGuardianshipStatus.Ended,
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
            WorldState.GetCharacterFamilyActionAffectedIds(tamperedPayload),
            tamperedPayload);
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(tampered));

        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterGuardianships.Guardianships);
    }

    [Fact]
    public void EventlessTurnAdvancesGuardianshipCalendarAndRejectsStalePlanAndEvent()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        EntityId commandId = new("command:test/guardianship-before-advance");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan stalePlan =
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId);
        CampaignEvent staleEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(stalePlan.ResolvedPayload),
            stalePlan.ResolvedPayload);

        Assert.Empty(simulation.ResolveTurn());
        Assert.True(simulation.World.Calendar.Date.CompareTo(Date) > 0);
        string afterAdvance = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterFamilyAction(
                CharacterFamilySystem.AuthoritativeActorId,
                new CharacterFamilyActionCommandPayload(
                    new EstablishPrimaryGuardianshipAction(Guardian, Ward, null)),
                Date,
                simulation.World.Calendar.TurnIndex,
                commandId,
                eventId));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(staleEvent));
        Assert.Equal(afterAdvance, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterGuardianships.Guardianships);
    }

    [Fact]
    public void PendingGuardianship_SaveLoadReplayIsDeterministic()
    {
        CampaignSimulation original = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)));
        CampaignCommand command = GuardianshipCommand(
            original,
            new EntityId("command:test/guardianship-pending-save"),
            Guardian,
            Ward);
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-guardianship-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "guardianship-pending.save.gz");
            new SaveStore().SaveAtomic(path, SaveEnvelope.Create("test", [], original));
            SaveEnvelope loaded = new SaveStore().Load(path);
            CampaignSimulation replay = new(WorldState.Restore(loaded.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsType<EstablishPrimaryGuardianshipAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(
                    Assert.Single(loaded.Snapshot.PendingCommands).Payload).Action);
            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SameTurnTwoGuardianRaceUsesEventIdOrderRegardlessOfSubmissionOrder()
    {
        (EntityId earlier, EntityId later) = OrderedCommandIds();
        for (int scenario = 0; scenario < 2; scenario++)
        {
            CampaignSimulation simulation = CreateCampaign(
                Seed(Guardian, new CampaignDate(160, 1, 1)),
                Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
                Seed(Ward, new CampaignDate(190, 1, 1)));
            EntityId earlierGuardian = scenario == 0 ? Guardian : OtherGuardian;
            EntityId laterGuardian = scenario == 0 ? OtherGuardian : Guardian;
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                later,
                laterGuardian,
                Ward)).IsValid);
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                earlier,
                earlierGuardian,
                Ward)).IsValid);

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            Assert.Equal(2, events.Count);
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
            Assert.True(simulation.World.CharacterGuardianships
                .TryGetActivePrimaryGuardianshipForWard(
                    Ward,
                    out CharacterGuardianshipState? active));
            Assert.Equal(earlierGuardian, active.GuardianCharacterId);
        }
    }

    [Fact]
    public void GuardianshipChecksumIsOrderInvariantMutationSensitiveAndCurrentSaveRetainsDiagnostics()
    {
        CampaignSimulation simulation = CreateCampaign(
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(OtherGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(190, 1, 1)),
            Seed(OtherWard, new CampaignDate(191, 1, 1)));
        Assert.True(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-current-save-a"),
            Guardian,
            Ward)).IsValid);
        Assert.True(simulation.Submit(GuardianshipCommand(
            simulation,
            new EntityId("command:test/guardianship-current-save-b"),
            OtherGuardian,
            OtherWard)).IsValid);
        Assert.Equal(2, simulation.ResolveTurn().Count);
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        WorldSnapshot shuffled = snapshot with
        {
            CharacterGuardianships = snapshot.CharacterGuardianships with
            {
                Guardianships = snapshot.CharacterGuardianships.Guardianships
                    .Reverse()
                    .ToArray(),
            },
        };
        CharacterGuardianshipState first = snapshot.CharacterGuardianships.Guardianships[0];
        WorldSnapshot mutated = snapshot with
        {
            CharacterGuardianships = snapshot.CharacterGuardianships with
            {
                Guardianships = snapshot.CharacterGuardianships.Guardianships
                    .Select(item => item.GuardianshipId == first.GuardianshipId
                        ? item with { EstablishedTurnIndex = item.EstablishedTurnIndex + 1 }
                        : item)
                    .ToArray(),
            },
        };

        Assert.Equal(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(shuffled));
        Assert.NotEqual(
            SimulationChecksum.Compute(snapshot),
            SimulationChecksum.Compute(mutated));

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-guardianship-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "guardianship-current.save.gz");
            new SaveStore().SaveAtomic(path, SaveEnvelope.Create("test", [], simulation));
            SaveEnvelope loaded = new SaveStore().Load(path);

            Assert.Equal(15, loaded.SchemaVersion);
            Assert.Contains(
                loaded.DiagnosticCommands,
                item => item.Payload is CharacterFamilyActionCommandPayload
                {
                    Action: EstablishPrimaryGuardianshipAction,
                });
            Assert.Contains(
                loaded.DiagnosticEvents,
                item => item.Payload is CharacterFamilyActionResolvedEventPayload
                {
                    Outcome: PrimaryGuardianshipEstablishedOutcome,
                });
            Assert.Equal(
                Serialize(snapshot.CharacterGuardianships),
                Serialize(loaded.Snapshot.CharacterGuardianships));
            Assert.Equal(SimulationChecksum.Compute(snapshot).Value, loaded.Checksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ThousandCharacterWorld_ResolvesBoundedGuardianshipBatchAndRecordsRawEvidence()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => Seed(
                new EntityId($"character:performance/guardianship-{index:D4}"),
                index == 0
                    ? new CampaignDate(150, 1, 1)
                    : new CampaignDate(190, 1, 1)))
            .ToArray();
        CampaignSimulation simulation = CreateCampaign(seeds);
        Stopwatch workflow = Stopwatch.StartNew();
        for (int index = 1; index <= 64; index++)
        {
            Assert.True(simulation.Submit(GuardianshipCommand(
                simulation,
                new EntityId($"command:performance/guardianship-{index:D2}"),
                seeds[0].Id,
                seeds[index].Id)).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch checksumWatch = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        checksumWatch.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(64, events.Count);
        Assert.All(events, item =>
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(item.Payload));
        Assert.Equal(64, snapshot.CharacterGuardianships.Guardianships.Count);
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"guardianship_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateCampaign(params CharacterSeed[] seeds) => new(
        WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(seeds),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty));

    private static CharacterWorldSnapshot CreateCharacters(CharacterSeed[] seeds)
    {
        CharacterDefinition[] definitions = seeds
            .Select(seed => new CharacterDefinition(
                CharacterContractVersions.Definition,
                seed.Id,
                new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                seed.BirthDate,
                [],
                [],
                [],
                [],
                [],
                new StructuredCharacterName(
                    new EntityId($"loc:{seed.Id.Value.Replace(':', '/')}"),
                    null),
                CharacterContentOrigin.LegacyUnknown(seed.Id),
                null,
                null,
                []))
            .OrderBy(item => item.Id)
            .ToArray();
        CharacterState[] states = seeds
            .Select(seed => new CharacterState(
                CharacterContractVersions.State,
                seed.Id,
                [],
                [],
                seed.Condition))
            .OrderBy(item => item.CharacterId)
            .ToArray();
        EntityId familyId = new("family:test/guardianship_all");
        EntityId householdId = new("household:test/guardianship_all");
        EntityId[] members = definitions.Select(item => item.Id).ToArray();
        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [
                new FamilyDefinition(
                    CharacterContractVersions.Definition,
                    familyId,
                    new EntityId("loc:family/guardianship_all")),
            ],
            [
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    householdId,
                    new EntityId("loc:household/guardianship_all")),
            ],
            states,
            [
                new FamilyState(
                    CharacterContractVersions.State,
                    familyId,
                    members),
            ],
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    householdId,
                    seeds[0].Id,
                    members),
            ]);
    }

    private static CampaignCommand GuardianshipCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        EntityId guardian,
        EntityId ward,
        EntityId? expected = null,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands) => CampaignCommand.Create(
        commandId,
        issuingActor ?? CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new EstablishPrimaryGuardianshipAction(guardian, ward, expected)),
        phase);

    private static (EntityId Earlier, EntityId Later) OrderedCommandIds()
    {
        EntityId first = new("command:test/two-guardian-first");
        EntityId second = new("command:test/two-guardian-second");
        return CharacterFamilyIds.DeriveActionEventId(Date, first).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, second)) < 0
                ? (first, second)
                : (second, first);
    }

    private static CharacterSeed Seed(EntityId id, CampaignDate birthDate) =>
        new(id, birthDate, CharacterConditionState.Default);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
