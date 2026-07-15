using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterComingOfAgeCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Ward = new("character:test/coming-of-age-ward");
    private static readonly EntityId Guardian = new("character:test/coming-of-age-guardian");
    private static readonly EntityId ReplacementGuardian =
        new("character:test/coming-of-age-replacement");
    private readonly ITestOutputHelper output;

    public CharacterComingOfAgeCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void GeneratedCommandAndPublicEventRoundTripAndEndActiveGuardianshipAtomically()
    {
        CharacterGuardianshipState active = ActiveGuardianship(Ward, Guardian, "exact-contract");
        CampaignSimulation simulation = CreateCampaign(
            Date,
            GeographicWorldSnapshot.Empty,
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [active]),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CampaignCommand external = CampaignCommand.Create(
            CharacterComingOfAgeIds.DeriveCommandId(Date, Ward),
            CharacterComingOfAgeSystem.AuthoritativeActorId,
            Date,
            new CharacterComingOfAgeCommandPayload(Ward, active.GuardianshipId),
            ResolutionPhase.Systems,
            CharacterComingOfAgeSystem.Priority);

        CommandValidationResult externalValidation = simulation.Submit(external);

        Assert.False(externalValidation.IsValid);
        Assert.Contains(
            externalValidation.Issues,
            issue => issue.Code == "system_generated_command");
        Assert.Empty(simulation.World.CaptureSnapshot().PendingCommands);
        WorldSnapshot before = simulation.World.CaptureSnapshot();

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterCameOfAgeEventPayload payload = Assert.IsType<
            CharacterCameOfAgeEventPayload>(campaignEvent.Payload);
        CharacterGuardianshipState ended = Assert.IsType<CharacterGuardianshipState>(
            payload.EndedPrimaryGuardianship);
        CampaignCommand generated = simulation.RecentCommands.Last();

        Assert.Equal(
            CharacterComingOfAgeIds.DeriveCommandId(Date, Ward),
            generated.CommandId);
        Assert.Equal(CharacterComingOfAgeSystem.AuthoritativeActorId, generated.IssuingActor);
        Assert.Equal("character_coming_of_age.v1", generated.CommandType);
        Assert.Equal(ResolutionPhase.Systems, generated.Phase);
        Assert.Equal(CharacterComingOfAgeSystem.Priority, generated.Priority);
        Assert.Equal(generated.CommandId, campaignEvent.CausalId);
        Assert.Equal(
            CharacterComingOfAgeIds.DeriveEventId(Date, generated.CommandId),
            campaignEvent.EventId);
        Assert.Equal(ResolutionPhase.Systems, campaignEvent.Phase);
        Assert.Equal(CharacterComingOfAgeSystem.Priority, campaignEvent.Priority);
        Assert.Equal("character_came_of_age.v1", campaignEvent.EventType);
        Assert.Equal(
            new[] { Ward, Guardian, active.GuardianshipId }.Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(CharacterGuardianshipStatus.Ended, ended.Status);
        Assert.Equal(CharacterGuardianshipEndReason.WardCameOfAge, ended.EndReason);
        Assert.Equal(Date, ended.EndDate);
        Assert.Equal(generated.CommandId, ended.EndSourceCommandId);
        Assert.Equal(campaignEvent.EventId, ended.EndSourceEventId);
        Assert.False(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        AssertOnlyGuardianshipSubsystemChanged(before, simulation.World.CaptureSnapshot());

        CampaignCommand commandRoundTrip = JsonSerializer.Deserialize<CampaignCommand>(
            Serialize(generated),
            SimulationJson.CreateOptions())!;
        CampaignEvent eventRoundTrip = JsonSerializer.Deserialize<CampaignEvent>(
            Serialize(campaignEvent),
            SimulationJson.CreateOptions())!;
        Assert.IsType<CharacterComingOfAgeCommandPayload>(commandRoundTrip.Payload);
        Assert.IsType<CharacterCameOfAgeEventPayload>(eventRoundTrip.Payload);

        WorldSnapshot forgedPending = before with
        {
            PendingCommands = [generated],
        };
        Assert.Throws<SimulationValidationException>(() =>
            WorldState.Restore(forgedPending));

        JsonObject commandJson = JsonNode.Parse(Serialize(generated))!.AsObject();
        JsonObject payloadJson = commandJson["payload"]!.AsObject();
        Assert.Equal("character_coming_of_age.v1", payloadJson["$type"]!.GetValue<string>());
        Assert.Equal(
            new[] { "$type", "characterId", "expectedActivePrimaryGuardianshipId" },
            payloadJson.Select(item => item.Key).Order());
        JsonObject eventJson = JsonNode.Parse(Serialize(campaignEvent))!.AsObject();
        JsonObject eventPayloadJson = eventJson["payload"]!.AsObject();
        Assert.Equal("character_came_of_age.v1", eventPayloadJson["$type"]!.GetValue<string>());
        Assert.Equal(
            new[] { "$type", "characterId", "endedPrimaryGuardianship" },
            eventPayloadJson.Select(item => item.Key).Order());
    }

    [Fact]
    public void ExactTransitionIncludesLaterTurnDayAndLeapDayButSkipsDeadAndBackfill()
    {
        CampaignDate leapTurnStart = new(202, 2, 27);
        EntityId firstDay = new("character:test/birthday-first");
        EntityId secondDay = new("character:test/birthday-second");
        EntityId leapDay = new("character:test/birthday-leap");
        EntityId dead = new("character:test/birthday-dead");
        EntityId alreadyAdult = new("character:test/birthday-backfill");
        CampaignSimulation simulation = CreateCampaign(
            leapTurnStart,
            GeographicWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            Seed(firstDay, new CampaignDate(184, 2, 27)),
            Seed(secondDay, new CampaignDate(184, 2, 28)),
            Seed(leapDay, new CampaignDate(184, 2, 29)),
            Seed(dead, new CampaignDate(184, 2, 28), DeadCondition()),
            Seed(alreadyAdult, new CampaignDate(170, 1, 1)));

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        CharacterCameOfAgeEventPayload[] birthdays = events
            .Select(item => Assert.IsType<CharacterCameOfAgeEventPayload>(item.Payload))
            .ToArray();
        Assert.Equal(3, birthdays.Length);
        Assert.Equal(
            new[] { firstDay, secondDay, leapDay }.Order(),
            birthdays.Select(item => item.CharacterId).Order());
        Assert.Equal(
            new[]
            {
                new CampaignDate(202, 2, 27),
                new CampaignDate(202, 2, 28),
                new CampaignDate(202, 3, 1),
            },
            events.Select(item => item.ResolutionDate));
        Assert.All(birthdays, item => Assert.Null(item.EndedPrimaryGuardianship));
        Assert.DoesNotContain(birthdays, item => item.CharacterId == dead);
        Assert.DoesNotContain(birthdays, item => item.CharacterId == alreadyAdult);
    }

    [Fact]
    public void CommandsPhaseRevocationAndPriorDayReplacementFeedExactBirthdayState()
    {
        CampaignDate dayBeforeBirthday = new(200, 5, 9);
        CharacterGuardianshipState revokeActive = ActiveGuardianship(
            Ward,
            Guardian,
            "same-day-revoke",
            dayBeforeBirthday.AddDays(-1));
        CampaignSimulation revoke = CreateCampaign(
            dayBeforeBirthday,
            GeographicWorldSnapshot.Empty,
            Snapshot(revokeActive),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CampaignCommand revokeCommand = FamilyCommand(
            new EntityId("command:test/revoke-on-birthday"),
            dayBeforeBirthday.AddDays(1),
            new EndPrimaryGuardianshipAction(
                Ward,
                revokeActive.GuardianshipId,
                CharacterGuardianshipEndReason.Revoked));
        Assert.True(revoke.Submit(revokeCommand).IsValid);

        IReadOnlyList<CampaignEvent> revokedEvents = revoke.ResolveTurn();

        CharacterCameOfAgeEventPayload revokedBirthday = Assert.IsType<
            CharacterCameOfAgeEventPayload>(revokedEvents.Single(
                item => item.Payload is CharacterCameOfAgeEventPayload).Payload);
        Assert.Null(revokedBirthday.EndedPrimaryGuardianship);
        Assert.Equal(
            CharacterGuardianshipEndReason.Revoked,
            revoke.World.CharacterGuardianships.Guardianships.Single().EndReason);

        CharacterGuardianshipState replacementActive = ActiveGuardianship(
            Ward,
            Guardian,
            "prior-day-replacement",
            dayBeforeBirthday.AddDays(-1));
        CampaignSimulation replace = CreateCampaign(
            dayBeforeBirthday,
            GeographicWorldSnapshot.Empty,
            Snapshot(replacementActive),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CampaignCommand replaceCommand = FamilyCommand(
            new EntityId("command:test/replace-before-birthday"),
            dayBeforeBirthday,
            new ReplacePrimaryGuardianshipAction(
                Ward,
                replacementActive.GuardianshipId,
                ReplacementGuardian));
        Assert.True(replace.Submit(replaceCommand).IsValid);

        IReadOnlyList<CampaignEvent> replacementEvents = replace.ResolveTurn();

        PrimaryGuardianshipReplacedOutcome replacement = Assert.IsType<
            PrimaryGuardianshipReplacedOutcome>(Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(replacementEvents.Single(
                    item => item.Payload is CharacterFamilyActionResolvedEventPayload).Payload).Outcome);
        CharacterCameOfAgeEventPayload replacementBirthday = Assert.IsType<
            CharacterCameOfAgeEventPayload>(replacementEvents.Single(
                item => item.Payload is CharacterCameOfAgeEventPayload).Payload);
        Assert.Equal(
            replacement.ReplacementGuardianship.GuardianshipId,
            replacementBirthday.EndedPrimaryGuardianship!.GuardianshipId);
        Assert.Equal(
            CharacterGuardianshipEndReason.WardCameOfAge,
            replacementBirthday.EndedPrimaryGuardianship.EndReason);

        CharacterGuardianshipState birthdayActive = ActiveGuardianship(
            Ward,
            Guardian,
            "birthday-replacement");
        CampaignSimulation birthdayReplacement = CreateCampaign(
            Date,
            GeographicWorldSnapshot.Empty,
            Snapshot(birthdayActive),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(ReplacementGuardian, new CampaignDate(159, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CampaignCommand birthdayReplaceCommand = FamilyCommand(
            new EntityId("command:test/replace-on-birthday"),
            Date,
            new ReplacePrimaryGuardianshipAction(
                Ward,
                birthdayActive.GuardianshipId,
                ReplacementGuardian));

        CommandValidationResult birthdayValidation = birthdayReplacement.Submit(
            birthdayReplaceCommand);
        IReadOnlyList<CampaignEvent> birthdayEvents = birthdayReplacement.ResolveTurn();

        Assert.False(birthdayValidation.IsValid);
        Assert.Contains(
            birthdayValidation.Issues,
            issue => issue.Code == "invalid_character_family_action");
        Assert.DoesNotContain(
            birthdayEvents,
            item => item.Payload is CharacterFamilyActionResolvedEventPayload);
        CharacterCameOfAgeEventPayload birthday = Assert.IsType<
            CharacterCameOfAgeEventPayload>(Assert.Single(birthdayEvents).Payload);
        Assert.Equal(
            birthdayActive.GuardianshipId,
            birthday.EndedPrimaryGuardianship!.GuardianshipId);
        Assert.Equal(
            CharacterGuardianshipEndReason.WardCameOfAge,
            birthday.EndedPrimaryGuardianship.EndReason);
        Assert.False(birthdayReplacement.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        Assert.Equal(
            birthday.EndedPrimaryGuardianship,
            Assert.Single(birthdayReplacement.World.CharacterGuardianships.Guardianships));
    }

    [Fact]
    public void ForgedStaleAndTamperedEventsRollBackAndExactReplayIsRejected()
    {
        CharacterGuardianshipState active = ActiveGuardianship(Ward, Guardian, "tamper");
        CampaignSimulation simulation = CreateCampaign(
            Date,
            GeographicWorldSnapshot.Empty,
            Snapshot(active),
            Seed(Guardian, new CampaignDate(160, 1, 1)),
            Seed(Ward, new CampaignDate(182, 5, 10)));
        CampaignCommand command = Assert.Single(
            simulation.World.PlanCharacterComingOfAgeCommands(Date));
        CharacterComingOfAgeCommandPayload commandPayload = Assert.IsType<
            CharacterComingOfAgeCommandPayload>(command.Payload);
        EntityId eventId = CharacterComingOfAgeIds.DeriveEventId(Date, command.CommandId);
        CharacterComingOfAgeResolutionPlan plan = simulation.World.PrepareCharacterComingOfAge(
            command.IssuingActor,
            commandPayload,
            Date,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CampaignEvent exact = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            Date,
            ResolutionPhase.Systems,
            CharacterComingOfAgeSystem.Priority,
            plan.AffectedIds,
            plan.Payload);
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterComingOfAge(
                new EntityId("system:test/forged"),
                commandPayload,
                Date,
                simulation.World.Calendar.TurnIndex,
                command.CommandId,
                eventId));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.PrepareCharacterComingOfAge(
                command.IssuingActor,
                commandPayload with { ExpectedActivePrimaryGuardianshipId = null },
                Date,
                simulation.World.Calendar.TurnIndex,
                command.CommandId,
                eventId));
        AssertRejectedWithoutMutation(simulation, exact with
        {
            Phase = ResolutionPhase.Commands,
        }, before);
        AssertRejectedWithoutMutation(simulation, exact with
        {
            Priority = CharacterComingOfAgeSystem.Priority + 1,
        }, before);
        AssertRejectedWithoutMutation(simulation, exact with
        {
            CausalId = new EntityId("command:test/forged-causal"),
        }, before);
        AssertRejectedWithoutMutation(simulation, exact with
        {
            EventId = new EntityId("event:test/forged"),
        }, before);
        AssertRejectedWithoutMutation(simulation, exact with
        {
            AffectedIds = [Ward],
        }, before);
        AssertRejectedWithoutMutation(simulation, exact with
        {
            Payload = plan.Payload with
            {
                EndedPrimaryGuardianship = plan.Payload.EndedPrimaryGuardianship! with
                {
                    EndTurnIndex = plan.Payload.EndedPrimaryGuardianship!.EndTurnIndex + 1,
                },
            },
        }, before);

        simulation.World.Apply(exact);
        string applied = Serialize(simulation.World.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(exact));
        Assert.Equal(applied, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void MultipleBirthdaysAndGeographyAreInputOrderDeterministic()
    {
        CharacterSeed[] seeds =
        [
            Seed(new EntityId("character:test/determinism-c"), new CampaignDate(182, 5, 10)),
            Seed(new EntityId("character:test/determinism-a"), new CampaignDate(182, 5, 10)),
            Seed(new EntityId("character:test/determinism-b"), new CampaignDate(182, 5, 10)),
        ];
        GeographicWorldSnapshot geography = GeographyFixture.Snapshot();
        GeographicWorldSnapshot shuffledGeography = geography with
        {
            Graph = geography.Graph with
            {
                Regions = geography.Graph.Regions.Reverse().ToArray(),
                Districts = geography.Graph.Districts.Reverse().ToArray(),
                Localities = geography.Graph.Localities.Reverse().ToArray(),
                Stops = geography.Graph.Stops.Reverse().ToArray(),
                Routes = geography.Graph.Routes.Reverse().ToArray(),
            },
            Locations = geography.Locations.Reverse().ToArray(),
            Routes = geography.Routes.Reverse().ToArray(),
            Armies = geography.Armies.Reverse().ToArray(),
        };
        CampaignSimulation ordered = CreateCampaign(
            Date,
            geography,
            CharacterGuardianshipWorldSnapshot.Empty,
            seeds.OrderBy(item => item.Id).ToArray());
        CampaignSimulation shuffled = CreateCampaign(
            Date,
            shuffledGeography,
            CharacterGuardianshipWorldSnapshot.Empty,
            seeds.Reverse().ToArray());

        IReadOnlyList<CampaignEvent> first = ordered.ResolveTurn();
        IReadOnlyList<CampaignEvent> second = shuffled.ResolveTurn();

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(
            SimulationChecksum.Compute(ordered.World.CaptureSnapshot()),
            SimulationChecksum.Compute(shuffled.World.CaptureSnapshot()));
        Assert.Equal(3, first.Count(item => item.Payload is CharacterCameOfAgeEventPayload));
        Assert.Contains(first, item => item.Payload is SupplyProducedEventPayload);
        CampaignEvent firstSystemEvent = first.First(item => item.ResolutionDate == Date);
        Assert.IsType<CharacterCameOfAgeEventPayload>(firstSystemEvent.Payload);
    }

    [Fact]
    public void SaveBeforeBirthdayRegeneratesExactEventAndSaveAfterNeverDuplicates()
    {
        CampaignDate start = new(200, 5, 9);
        CampaignSimulation original = CreateCampaign(
            start,
            GeographicWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            Seed(Ward, new CampaignDate(182, 5, 10)));
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-coming-of-age-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string beforePath = Path.Combine(directory, "before.save.gz");
            new SaveStore().SaveAtomic(
                beforePath,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope beforeLoad = new SaveStore().Load(beforePath);
            Assert.Empty(beforeLoad.Snapshot.PendingCommands);
            CampaignSimulation replay = new(WorldState.Restore(beforeLoad.Snapshot));

            IReadOnlyList<CampaignEvent> first = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> second = replay.ResolveTurn();

            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));
            Assert.Single(first, item => item.Payload is CharacterCameOfAgeEventPayload);

            string afterPath = Path.Combine(directory, "after.save.gz");
            new SaveStore().SaveAtomic(
                afterPath,
                SaveEnvelope.Create("test", [], original));
            SaveEnvelope afterLoad = new SaveStore().Load(afterPath);
            Assert.Contains(
                afterLoad.DiagnosticCommands,
                item => item.Payload is CharacterComingOfAgeCommandPayload);
            Assert.Contains(
                afterLoad.DiagnosticEvents,
                item => item.Payload is CharacterCameOfAgeEventPayload);
            CampaignSimulation afterReplay = new(WorldState.Restore(afterLoad.Snapshot));
            Assert.DoesNotContain(
                afterReplay.ResolveTurn(),
                item => item.Payload is CharacterCameOfAgeEventPayload);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ThousandCharacterBirthdayBatchRecordsRawComponentEvidence()
    {
        CharacterSeed[] seeds = Enumerable.Range(0, 1_000)
            .Select(index => Seed(
                new EntityId($"character:performance/coming-of-age-{index:D4}"),
                new CampaignDate(182, 5, 10)))
            .ToArray();
        CampaignSimulation simulation = CreateCampaign(
            Date,
            GeographicWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            seeds);
        Stopwatch workflow = Stopwatch.StartNew();

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

        Assert.Equal(1_000, events.Count);
        Assert.All(events, item => Assert.IsType<CharacterCameOfAgeEventPayload>(item.Payload));
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.Empty(snapshot.CharacterGuardianships.Guardianships);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"coming_of_age_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static void AssertRejectedWithoutMutation(
        CampaignSimulation simulation,
        CampaignEvent campaignEvent,
        string expectedSnapshot)
    {
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(campaignEvent));
        Assert.Equal(expectedSnapshot, Serialize(simulation.World.CaptureSnapshot()));
    }

    private static void AssertOnlyGuardianshipSubsystemChanged(
        WorldSnapshot before,
        WorldSnapshot after)
    {
        Assert.Equal(Serialize(before.Geography), Serialize(after.Geography));
        Assert.Equal(Serialize(before.Characters), Serialize(after.Characters));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(
            Serialize(before.CharacterEstateHoldings),
            Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
        Assert.Equal(Serialize(before.RandomStreams), Serialize(after.RandomStreams));
        Assert.Equal(Serialize(before.SystemVersions), Serialize(after.SystemVersions));
        Assert.Equal(Serialize(before.PendingCommands), Serialize(after.PendingCommands));
    }

    private static CampaignSimulation CreateCampaign(
        CampaignDate startDate,
        GeographicWorldSnapshot geography,
        CharacterGuardianshipWorldSnapshot guardianships,
        params CharacterSeed[] seeds) => new(
        WorldState.Create(
            startDate,
            20260716,
            [],
            geography,
            CreateCharacters(seeds),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            guardianships));

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
            .ToArray();
        CharacterState[] states = seeds
            .Select(seed => new CharacterState(
                CharacterContractVersions.State,
                seed.Id,
                [],
                [],
                seed.Condition))
            .ToArray();
        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
    }

    private static CharacterGuardianshipWorldSnapshot Snapshot(
        CharacterGuardianshipState active) => new(
        CharacterGuardianshipContractVersions.Snapshot,
        [active]);

    private static CharacterGuardianshipState ActiveGuardianship(
        EntityId ward,
        EntityId guardian,
        string suffix,
        CampaignDate? establishedDate = null)
    {
        CampaignDate actualDate = establishedDate ?? Date.AddDays(-1);
        EntityId commandId = new($"command:test/{suffix}");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(actualDate, commandId);
        return new CharacterGuardianshipState(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(eventId, ward, guardian),
            ward,
            guardian,
            actualDate,
            0,
            commandId,
            eventId,
            CharacterGuardianshipStatus.Active,
            null,
            null,
            null,
            null,
            null);
    }

    private static CampaignCommand FamilyCommand(
        EntityId commandId,
        CampaignDate date,
        ICharacterFamilyAction action) => CampaignCommand.Create(
        commandId,
        CharacterFamilySystem.AuthoritativeActorId,
        date,
        new CharacterFamilyActionCommandPayload(action));

    private static CharacterConditionState DeadCondition() => new(
        CharacterVitalStatus.Dead,
        CharacterHealthStatus.Critical,
        IsIncapacitated: true,
        CharacterCustodyStatus.Free,
        null);

    private static CharacterSeed Seed(
        EntityId id,
        CampaignDate birthDate,
        CharacterConditionState? condition = null) => new(
        id,
        birthDate,
        condition ?? CharacterConditionState.Default);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(
        EntityId Id,
        CampaignDate BirthDate,
        CharacterConditionState Condition);
}
