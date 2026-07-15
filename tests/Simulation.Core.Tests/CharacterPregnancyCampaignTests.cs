using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterPregnancyCampaignTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId FirstParent = new("character:test/pregnancy-first");
    private static readonly EntityId SecondParent = new("character:test/pregnancy-second");
    private static readonly EntityId ThirdParent = new("character:test/pregnancy-third");
    private static readonly EntityId FourthParent = new("character:test/pregnancy-fourth");
    private static readonly EntityId PracticeId = new("marriage_practice:test/pregnancy");
    private readonly ITestOutputHelper output;

    public CharacterPregnancyCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void RegistrationEnforcesAuthorityAndPhaseAndMutatesOnlyPregnancies()
    {
        UnionSeed union = new("success", FirstParent, SecondParent);
        CampaignSimulation simulation = CreateCampaign(
            [Adult(FirstParent), Adult(SecondParent)],
            [union]);
        Assert.False(simulation.Submit(Command(
            simulation,
            "unauthorized",
            FirstParent,
            SecondParent,
            union.UnionId,
            issuingActor: FirstParent)).IsValid);
        Assert.False(simulation.Submit(Command(
            simulation,
            "wrong-phase",
            FirstParent,
            SecondParent,
            union.UnionId,
            phase: ResolutionPhase.Systems)).IsValid);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = Command(
            simulation,
            "success",
            FirstParent,
            SecondParent,
            union.UnionId);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        RegisterActivePregnancyAction action = Assert.IsType<
            RegisterActivePregnancyAction>(payload.Action);
        CharacterPregnancyState pregnancy = Assert.IsType<
            ActivePregnancyRegisteredOutcome>(payload.Outcome).Pregnancy;

        Assert.Equal(CharacterFamilySystem.AuthoritativeActorId, payload.ActingActorId);
        Assert.Equal(command.CommandId, campaignEvent.CausalId);
        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(Date.AddDays(CharacterPregnancyLimits.GestationDays), pregnancy.ExpectedBirthDate);
        Assert.Equal(action.GestationalParentCharacterId, pregnancy.GestationalParentCharacterId);
        Assert.Equal(action.OtherBiologicalParentCharacterId, pregnancy.OtherBiologicalParentCharacterId);
        Assert.Equal(action.SourceUnionId, pregnancy.SourceUnionId);
        Assert.Equal(
            CharacterPregnancyIds.DerivePregnancyId(
                campaignEvent.EventId,
                FirstParent,
                SecondParent,
                union.UnionId),
            pregnancy.PregnancyId);
        Assert.Equal(
            new[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                pregnancy.PregnancyId,
                FirstParent,
                SecondParent,
                union.UnionId,
            }.Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.CharacterPregnancies
            .TryGetActivePregnancyForGestationalParent(
                FirstParent,
                out CharacterPregnancyState? active));
        Assert.Equal(pregnancy, active);

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        AssertUnchangedNonPregnancySubsystems(before, after);
        Assert.Equal(before.RandomStreams, after.RandomStreams);
    }

    [Fact]
    public void RacesFollowCanonicalPriorityThenEventIdOrderInBothSubmissionOrders()
    {
        UnionSeed firstUnion = new("race-first", FirstParent, SecondParent);
        UnionSeed secondUnion = new("race-second", FirstParent, ThirdParent);
        (string earlier, string later) = OrderedSuffixes("parent-race-a", "parent-race-b");
        for (int assignment = 0; assignment < 2; assignment++)
        {
            UnionSeed earlierUnion = assignment == 0 ? firstUnion : secondUnion;
            UnionSeed laterUnion = assignment == 0 ? secondUnion : firstUnion;
            EntityId earlierOtherParent = assignment == 0 ? SecondParent : ThirdParent;
            EntityId laterOtherParent = assignment == 0 ? ThirdParent : SecondParent;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation parentRace = CreateCampaign(
                    [Adult(FirstParent), Adult(SecondParent), Adult(ThirdParent)],
                    [firstUnion, secondUnion]);
                CampaignCommand earlierCommand = Command(
                    parentRace,
                    earlier,
                    FirstParent,
                    earlierOtherParent,
                    earlierUnion.UnionId);
                CampaignCommand laterCommand = Command(
                    parentRace,
                    later,
                    FirstParent,
                    laterOtherParent,
                    laterUnion.UnionId);
                CampaignCommand[] submissions = submissionOrder == 0
                    ? [earlierCommand, laterCommand]
                    : [laterCommand, earlierCommand];
                foreach (CampaignCommand command in submissions)
                {
                    Assert.True(parentRace.Submit(command).IsValid);
                }

                IReadOnlyList<CampaignEvent> parentEvents = parentRace.ResolveTurn();

                Assert.Equal(2, parentEvents.Count);
                Assert.IsType<CharacterFamilyActionResolvedEventPayload>(parentEvents[0].Payload);
                Assert.IsType<CommandCancelledEventPayload>(parentEvents[1].Payload);
                Assert.Equal(
                    earlierUnion.UnionId,
                    Assert.Single(parentRace.World.CharacterPregnancies.ActivePregnancies)
                        .SourceUnionId);
            }
        }

        UnionSeed sharedUnion = new("shared-race", FirstParent, SecondParent);
        (earlier, later) = OrderedSuffixes("union-race-a", "union-race-b");
        for (int assignment = 0; assignment < 2; assignment++)
        {
            EntityId earlierGestationalParent = assignment == 0
                ? FirstParent
                : SecondParent;
            EntityId laterGestationalParent = assignment == 0
                ? SecondParent
                : FirstParent;
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation unionRace = CreateCampaign(
                    [Adult(FirstParent), Adult(SecondParent)],
                    [sharedUnion]);
                CampaignCommand earlierCommand = Command(
                    unionRace,
                    earlier,
                    earlierGestationalParent,
                    laterGestationalParent,
                    sharedUnion.UnionId);
                CampaignCommand laterCommand = Command(
                    unionRace,
                    later,
                    laterGestationalParent,
                    earlierGestationalParent,
                    sharedUnion.UnionId);
                CampaignCommand[] submissions = submissionOrder == 0
                    ? [earlierCommand, laterCommand]
                    : [laterCommand, earlierCommand];
                foreach (CampaignCommand command in submissions)
                {
                    Assert.True(unionRace.Submit(command).IsValid);
                }

                IReadOnlyList<CampaignEvent> unionEvents = unionRace.ResolveTurn();

                Assert.Equal(2, unionEvents.Count);
                Assert.IsType<CharacterFamilyActionResolvedEventPayload>(unionEvents[0].Payload);
                Assert.IsType<CommandCancelledEventPayload>(unionEvents[1].Payload);
                Assert.Equal(
                    earlierGestationalParent,
                    Assert.Single(unionRace.World.CharacterPregnancies.ActivePregnancies)
                        .GestationalParentCharacterId);
            }
        }

        (string eventEarlier, string eventLater) = OrderedSuffixes(
            "priority-race-a",
            "priority-race-b");
        for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
        {
            CampaignSimulation priorityRace = CreateCampaign(
                [Adult(FirstParent), Adult(SecondParent), Adult(ThirdParent)],
                [firstUnion, secondUnion]);
            CampaignCommand earlierEventCommand = Command(
                priorityRace,
                eventEarlier,
                FirstParent,
                SecondParent,
                firstUnion.UnionId,
                priority: 10);
            CampaignCommand laterEventCommand = Command(
                priorityRace,
                eventLater,
                FirstParent,
                ThirdParent,
                secondUnion.UnionId,
                priority: -10);
            CampaignCommand[] submissions = submissionOrder == 0
                ? [earlierEventCommand, laterEventCommand]
                : [laterEventCommand, earlierEventCommand];
            foreach (CampaignCommand command in submissions)
            {
                Assert.True(priorityRace.Submit(command).IsValid);
            }

            IReadOnlyList<CampaignEvent> priorityEvents = priorityRace.ResolveTurn();

            Assert.Equal(
                CharacterFamilyIds.DeriveActionEventId(Date, laterEventCommand.CommandId),
                priorityEvents[0].EventId);
            Assert.IsType<CharacterFamilyActionResolvedEventPayload>(priorityEvents[0].Payload);
            Assert.IsType<CommandCancelledEventPayload>(priorityEvents[1].Payload);
            Assert.Equal(
                secondUnion.UnionId,
                Assert.Single(priorityRace.World.CharacterPregnancies.ActivePregnancies)
                    .SourceUnionId);
        }
    }

    [Fact]
    public void IndependentRegistrationsAreInputOrderInvariantAndChecksumDeterministic()
    {
        UnionSeed firstUnion = new("independent-first", FirstParent, SecondParent);
        UnionSeed secondUnion = new("independent-second", ThirdParent, FourthParent);
        CharacterSeed[] characters =
        [
            Adult(FirstParent),
            Adult(SecondParent),
            Adult(ThirdParent),
            Adult(FourthParent),
        ];
        UnionSeed[] unions = [firstUnion, secondUnion];
        CampaignSimulation forward = CreateCampaign(characters, unions);
        CampaignSimulation reverse = CreateCampaign(characters.Reverse().ToArray(), unions.Reverse().ToArray());
        CampaignCommand firstForward = Command(
            forward,
            "independent-first",
            FirstParent,
            SecondParent,
            firstUnion.UnionId);
        CampaignCommand secondForward = Command(
            forward,
            "independent-second",
            ThirdParent,
            FourthParent,
            secondUnion.UnionId);
        CampaignCommand firstReverse = firstForward with { };
        CampaignCommand secondReverse = secondForward with { };

        Assert.True(forward.Submit(firstForward).IsValid);
        Assert.True(forward.Submit(secondForward).IsValid);
        Assert.True(reverse.Submit(secondReverse).IsValid);
        Assert.True(reverse.Submit(firstReverse).IsValid);

        IReadOnlyList<CampaignEvent> forwardEvents = forward.ResolveTurn();
        IReadOnlyList<CampaignEvent> reverseEvents = reverse.ResolveTurn();

        Assert.Equal(Serialize(forwardEvents), Serialize(reverseEvents));
        Assert.Equal(
            Serialize(forward.World.CharacterPregnancies.CaptureSnapshot()),
            Serialize(reverse.World.CharacterPregnancies.CaptureSnapshot()));
        Assert.Equal(
            SimulationChecksum.Compute(forward.World.CaptureSnapshot()),
            SimulationChecksum.Compute(reverse.World.CaptureSnapshot()));
    }

    [Fact]
    public void TamperedIdentityAffectedIdsPayloadAndReplayRollBackCompletely()
    {
        UnionSeed union = new("tamper", FirstParent, SecondParent);
        CampaignSimulation simulation = CreateCampaign(
            [Adult(FirstParent), Adult(SecondParent)],
            [union]);
        EntityId commandId = new("command:test/pregnancy-tamper");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(Date, commandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            new CharacterFamilyActionCommandPayload(new RegisterActivePregnancyAction(
                FirstParent,
                SecondParent,
                union.UnionId,
                null)),
            Date,
            simulation.World.Calendar.TurnIndex,
            commandId,
            eventId);
        ActivePregnancyRegisteredOutcome outcome = Assert.IsType<
            ActivePregnancyRegisteredOutcome>(plan.ResolvedPayload.Outcome);
        CampaignEvent exact = new(
            ContractVersions.CampaignEvent,
            eventId,
            commandId,
            Date,
            ResolutionPhase.Commands,
            0,
            WorldState.GetCharacterFamilyActionAffectedIds(plan.ResolvedPayload),
            plan.ResolvedPayload);
        EntityId forgedCommandId = new("command:test/pregnancy-tamper-forged");
        CampaignEvent[] identityTampering =
        [
            exact with { CausalId = forgedCommandId },
            exact with
            {
                EventId = CharacterFamilyIds.DeriveActionEventId(Date, forgedCommandId),
            },
            exact with
            {
                AffectedIds = exact.AffectedIds.Take(exact.AffectedIds.Count - 1).ToArray(),
            },
        ];
        string before = Serialize(simulation.World.CaptureSnapshot());
        foreach (CampaignEvent tamperedIdentity in identityTampering)
        {
            Assert.Throws<SimulationValidationException>(() =>
                simulation.World.Apply(tamperedIdentity));
            Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
            Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        }

        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                Pregnancy = outcome.Pregnancy with
                {
                    ExpectedBirthDate = outcome.Pregnancy.ExpectedBirthDate.AddDays(1),
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
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(tampered));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);

        simulation.World.Apply(exact);
        string after = Serialize(simulation.World.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(exact));
        Assert.Equal(after, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void PendingAndResolvedPregnancyRoundTripThroughJsonAndGzip()
    {
        UnionSeed union = new("round-trip", FirstParent, SecondParent);
        CampaignSimulation original = CreateCampaign(
            [Adult(FirstParent), Adult(SecondParent)],
            [union]);
        Assert.True(original.Submit(Command(
            original,
            "round-trip",
            FirstParent,
            SecondParent,
            union.UnionId)).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-pregnancy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "pregnancy.save.gz");
            SaveStore store = new();
            store.SaveAtomic(path, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(path);
            CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));

            IReadOnlyList<CampaignEvent> expected = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> actual = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
            Assert.IsType<RegisterActivePregnancyAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(Assert.Single(
                    pending.Snapshot.PendingCommands).Payload).Action);
            Assert.Equal(Serialize(expected), Serialize(actual));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));

            SaveEnvelope resolved = SaveEnvelope.Create("test", [], original);
            string resolvedPath = Path.Combine(directory, "pregnancy-resolved.save.gz");
            store.SaveAtomic(resolvedPath, resolved);
            SaveEnvelope gzipRoundTrip = store.Load(resolvedPath);
            Assert.Equal(resolved.Checksum, gzipRoundTrip.Checksum);
            Assert.Equal(
                SimulationChecksum.Compute(gzipRoundTrip.Snapshot).Value,
                gzipRoundTrip.Checksum);
            CharacterPregnancyState gzipPregnancy = Assert.Single(
                gzipRoundTrip.Snapshot.CharacterPregnancies.ActivePregnancies);
            ActivePregnancyRegisteredOutcome gzipOutcome = Assert.IsType<
                ActivePregnancyRegisteredOutcome>(Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(gzipRoundTrip.DiagnosticEvents
                        .Single(item => item.Payload is CharacterFamilyActionResolvedEventPayload)
                        .Payload).Outcome);
            Assert.Equal(gzipPregnancy, gzipOutcome.Pregnancy);

            SaveEnvelope jsonRoundTrip = JsonSerializer.Deserialize<SaveEnvelope>(
                Serialize(resolved),
                SimulationJson.CreateOptions())!;
            Assert.Single(jsonRoundTrip.Snapshot.CharacterPregnancies.ActivePregnancies);
            ActivePregnancyRegisteredOutcome restoredOutcome = Assert.IsType<
                ActivePregnancyRegisteredOutcome>(Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(jsonRoundTrip.DiagnosticEvents
                        .Single(item => item.Payload is CharacterFamilyActionResolvedEventPayload)
                        .Payload).Outcome);
            Assert.Equal(
                jsonRoundTrip.Snapshot.CharacterPregnancies.ActivePregnancies[0],
                restoredOutcome.Pregnancy);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExpectedBirthDateDoesNotAutoResolveAndThousandCharacterFixtureIsBounded()
    {
        CharacterSeed[] characters = Enumerable.Range(0, 1_000)
            .Select(index => Adult(new EntityId($"character:pregnancy-performance/{index:D4}")))
            .ToArray();
        UnionSeed union = new("performance", characters[0].Id, characters[1].Id);
        CampaignSimulation simulation = CreateCampaign(characters, [union]);
        Stopwatch workflow = Stopwatch.StartNew();
        Assert.True(simulation.Submit(Command(
            simulation,
            "performance",
            characters[0].Id,
            characters[1].Id,
            union.UnionId)).IsValid);
        Assert.Single(simulation.ResolveTurn());
        workflow.Stop();
        CharacterPregnancyState pregnancy = Assert.Single(
            simulation.World.CharacterPregnancies.ActivePregnancies);

        while (simulation.World.Calendar.Date.CompareTo(pregnancy.ExpectedBirthDate) <= 0)
        {
            Assert.Empty(simulation.ResolveTurn());
        }

        Assert.Equal(pregnancy, Assert.Single(
            simulation.World.CharacterPregnancies.ActivePregnancies));
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        Stopwatch checksumWatch = Stopwatch.StartNew();
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

        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"pregnancy_raw workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateCampaign(
        IReadOnlyList<CharacterSeed> characterSeeds,
        IReadOnlyList<UnionSeed> unionSeeds)
    {
        CharacterDefinition[] definitions = characterSeeds
            .OrderBy(item => item.Id)
            .Select(item =>
            {
                EntityId nameKey = new($"loc:{item.Id.Value.Replace(':', '/')}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    item.Id,
                    nameKey,
                    item.BirthDate,
                    [],
                    [],
                    [],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(item.Id),
                    null,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = characterSeeds
            .OrderBy(item => item.Id)
            .Select(item => new CharacterState(
                CharacterContractVersions.State,
                item.Id,
                [],
                [],
                CharacterConditionState.Default))
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        MarriagePracticeState practice = new(
            CharacterMarriageContractVersions.Practice,
            PracticeId,
            18,
            18,
            8,
            64,
            64,
            true,
            true,
            MarriageProhibitedKinship.None);
        MarriageProposalState[] proposals = unionSeeds
            .Select(Proposal)
            .OrderBy(item => item.ProposalId)
            .ToArray();
        Dictionary<string, MarriageProposalState> proposalBySuffix = proposals
            .ToDictionary(item => item.ProposalId.Value.Split('/').Last(), StringComparer.Ordinal);
        MarriageUnionState[] unions = unionSeeds
            .Select(item => Union(item, proposalBySuffix[item.Suffix]))
            .OrderBy(item => item.UnionId)
            .ToArray();
        CharacterMarriageWorldSnapshot marriages = new(
            CharacterMarriageContractVersions.Snapshot,
            [practice],
            proposals,
            [],
            unions,
            [],
            [],
            []);
        return new CampaignSimulation(WorldState.Create(
            Date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            marriages,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty));
    }

    private static MarriageProposalState Proposal(UnionSeed seed) => new(
        CharacterMarriageContractVersions.State,
        new EntityId($"marriage_proposal:test/pregnancy/{seed.Suffix}"),
        MarriageProposalKind.LegalUnion,
        MarriageBasis.Political,
        MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind.PoliticalArrangement,
        seed.First.CompareTo(seed.Second) < 0 ? seed.First : seed.Second,
        seed.First.CompareTo(seed.Second) < 0 ? seed.Second : seed.First,
        null,
        PracticeId,
        Date.AddDays(-2),
        0,
        new EntityId($"command:test/pregnancy-proposal-{seed.Suffix}"),
        MarriageProposalStatus.Accepted,
        Date.AddDays(-1),
        0,
        new EntityId($"command:test/pregnancy-accept-{seed.Suffix}"));

    private static MarriageUnionState Union(
        UnionSeed seed,
        MarriageProposalState proposal) => new(
        CharacterMarriageContractVersions.State,
        seed.UnionId,
        proposal.ProposerCharacterId,
        proposal.RecipientCharacterId,
        proposal.ProposedForm,
        null,
        proposal.Basis,
        proposal.ConsentKind,
        PracticeId,
        proposal.ProposalId,
        proposal.ResolutionDate!.Value,
        proposal.ResolutionTurnIndex!.Value,
        MarriageUnionStatus.Active,
        null,
        null,
        null,
        null);

    private static CampaignCommand Command(
        CampaignSimulation simulation,
        string suffix,
        EntityId gestationalParent,
        EntityId otherParent,
        EntityId unionId,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/pregnancy-{suffix}"),
        issuingActor ?? CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(new RegisterActivePregnancyAction(
            gestationalParent,
            otherParent,
            unionId,
            null)),
        phase,
        priority);

    private static (string Earlier, string Later) OrderedSuffixes(
        string first,
        string second)
    {
        EntityId firstCommand = new($"command:test/pregnancy-{first}");
        EntityId secondCommand = new($"command:test/pregnancy-{second}");
        return CharacterFamilyIds.DeriveActionEventId(Date, firstCommand).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, secondCommand)) < 0
                ? (first, second)
                : (second, first);
    }

    private static CharacterSeed Adult(EntityId id) => new(
        id,
        new CampaignDate(170, 1, 1));

    private static void AssertUnchangedNonPregnancySubsystems(
        WorldSnapshot before,
        WorldSnapshot after)
    {
        Assert.Equal(Serialize(before.Geography), Serialize(after.Geography));
        Assert.Equal(Serialize(before.Characters), Serialize(after.Characters));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(Serialize(before.CharacterEstateHoldings), Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.CharacterGuardianships), Serialize(after.CharacterGuardianships));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record CharacterSeed(EntityId Id, CampaignDate BirthDate);

    private sealed record UnionSeed(string Suffix, EntityId First, EntityId Second)
    {
        public EntityId UnionId { get; } = new($"marriage_union:test/pregnancy/{Suffix}");
    }
}
