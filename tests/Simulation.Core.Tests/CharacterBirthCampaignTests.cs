using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterBirthCampaignTests
{
    private static readonly CampaignDate PregnancyStart = new(200, 1, 1);
    private static readonly CampaignDate DueDate = PregnancyStart.AddDays(
        CharacterPregnancyLimits.GestationDays);
    private static readonly EntityId FirstParent = new("character:test/birth-first");
    private static readonly EntityId SecondParent = new("character:test/birth-second");
    private static readonly EntityId ThirdParent = new("character:test/birth-third");
    private static readonly EntityId FourthParent = new("character:test/birth-fourth");
    private static readonly EntityId PracticeId = new("marriage_practice:test/birth");
    private static readonly EntityId FamilyId = new("family:test/birth");
    private static readonly EntityId HouseholdId = new("household:test/birth");
    private static readonly EntityId CultureId = new("culture:test/birth");
    private static readonly EntityId TraitId = new("trait:test/birth");
    private readonly ITestOutputHelper output;

    public CharacterBirthCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void BirthContractsRoundTripDefensivelyWithStableIdGoldens()
    {
        List<EntityId> sourceTraits = [TraitId];
        GeneratedNewbornSpecification newborn = new(
            CharacterBirthContractVersions.NewbornSpecification,
            new EntityId("loc:test/birth_contract"),
            CultureId,
            FamilyId,
            HouseholdId,
            sourceTraits);
        ResolvePregnancyBirthAction action = new(
            new EntityId("pregnancy:test/birth-golden"),
            newborn);
        sourceTraits.Clear();

        string actionJson = JsonSerializer.Serialize<ICharacterFamilyAction>(
            action,
            SimulationJson.CreateOptions());
        ICharacterFamilyAction restoredAction = JsonSerializer.Deserialize<
            ICharacterFamilyAction>(
                actionJson,
                SimulationJson.CreateOptions())!;

        Assert.Equal([TraitId], newborn.InheritedTraitIds);
        Assert.IsType<ResolvePregnancyBirthAction>(restoredAction);
        Assert.Contains("resolve_pregnancy_birth.v1", actionJson, StringComparison.Ordinal);
        Assert.Equal(
            "character_birth:sha256/b4b0f9b8d0662abf14839e66d9a27a613420848b46dc46f3db18a96a022f5d77",
            CharacterBirthIds.DeriveBirthId(
                new EntityId("event:test/birth-golden"),
                action.ExpectedPregnancyId).Value);
        Assert.Equal(
            "character:sha256/7e48a4001ce31b1e8c7fac565c1a251416972a59c4bef60f16ed9770ae50ed03",
            CharacterBirthIds.DeriveChildId(action.ExpectedPregnancyId).Value);

        BirthSeed seed = new("contract", FirstParent, SecondParent);
        CampaignSimulation simulation = CreateCampaign(
            [FirstParent, SecondParent],
            [seed]);
        CampaignCommand command = BirthCommand(simulation, seed, "contract");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(DueDate, command.CommandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            Assert.IsType<CharacterFamilyActionCommandPayload>(command.Payload),
            DueDate,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        string outcomeJson = JsonSerializer.Serialize<ICharacterFamilyActionOutcome>(
            plan.ResolvedPayload.Outcome,
            SimulationJson.CreateOptions());
        ICharacterFamilyActionOutcome restoredOutcome = JsonSerializer.Deserialize<
            ICharacterFamilyActionOutcome>(
                outcomeJson,
                SimulationJson.CreateOptions())!;

        Assert.IsType<PregnancyBirthResolvedOutcome>(restoredOutcome);
        Assert.Contains("pregnancy_birth_resolved.v1", outcomeJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DueBirthResolvesAtomicallyWithExactAffectedIdsAndQueries()
    {
        BirthSeed seed = new("success", FirstParent, SecondParent);
        CampaignSimulation simulation = CreateCampaign(
            [FirstParent, SecondParent],
            [seed]);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = BirthCommand(simulation, seed, "success");

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        CharacterBirthChange birth = Assert.IsType<
            PregnancyBirthResolvedOutcome>(payload.Outcome).Birth;

        Assert.Equal(CharacterBirthContractVersions.Change, birth.ContractVersion);
        Assert.Equal(seed.PregnancyId, birth.ResolvedPregnancy.PregnancyId);
        Assert.Equal(DueDate, birth.ChildDefinition.BirthDate);
        Assert.Equal(DueDate, birth.ResolutionDate);
        Assert.Equal(CharacterBirthIds.DeriveChildId(seed.PregnancyId), birth.ChildDefinition.Id);
        Assert.Equal(
            CharacterBirthIds.DeriveBirthId(campaignEvent.EventId, seed.PregnancyId),
            birth.BirthId);
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            birth.ChildDefinition.Id,
            out AuthoritativeCharacterProfile? child));
        Assert.Equal(CharacterOriginKind.Generated, child.ContentOrigin.OriginKind);
        Assert.Equal(CharacterHistoricalClassification.Fictional, child.ContentOrigin.HistoricalClassification);
        Assert.Equal(birth.BirthId, child.ContentOrigin.RecordId);
        Assert.Equal(FamilyId, child.FamilyId);
        Assert.Equal(HouseholdId, child.HouseholdId);
        Assert.Equal(CultureId, child.CultureId);
        Assert.Equal([TraitId], child.TraitIds);
        Assert.Equal(2, child.ParentLinks.Count);
        Assert.All(
            child.ParentLinks,
            link => Assert.Equal(ParentChildLinkKind.Biological, link.Kind));
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            FirstParent,
            out AuthoritativeCharacterProfile? firstParent));
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            SecondParent,
            out AuthoritativeCharacterProfile? secondParent));
        Assert.Contains(firstParent.ChildLinks, link =>
            link.ChildCharacterId == child.CharacterId
            && link.Kind == ParentChildLinkKind.Biological);
        Assert.Contains(secondParent.ChildLinks, link =>
            link.ChildCharacterId == child.CharacterId
            && link.Kind == ParentChildLinkKind.Biological);
        Assert.Equal(
            new[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                birth.BirthId,
                seed.PregnancyId,
                child.CharacterId,
                FirstParent,
                SecondParent,
                seed.UnionId,
                FamilyId,
                HouseholdId,
            }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.Equal(Serialize(before.RandomStreams), Serialize(after.RandomStreams));
        Assert.Equal(Serialize(before.Geography), Serialize(after.Geography));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(Serialize(before.CharacterEstateHoldings), Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.CharacterGuardianships), Serialize(after.CharacterGuardianships));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
    }

    [Fact]
    public void SamePregnancyRaceUsesCanonicalEventOrderInBothAssignments()
    {
        BirthSeed seed = new("race", FirstParent, SecondParent);
        EntityId firstCommandId = new("command:test/birth-race-a");
        EntityId secondCommandId = new("command:test/birth-race-b");
        EntityId earlier = CharacterFamilyIds.DeriveActionEventId(DueDate, firstCommandId)
            .CompareTo(CharacterFamilyIds.DeriveActionEventId(DueDate, secondCommandId)) < 0
                ? firstCommandId
                : secondCommandId;
        EntityId later = earlier == firstCommandId ? secondCommandId : firstCommandId;
        for (int assignment = 0; assignment < 2; assignment++)
        {
            EntityId earlierName = new($"loc:test/birth_race_{assignment}");
            EntityId laterName = new($"loc:test/birth_race_{1 - assignment}");
            for (int submissionOrder = 0; submissionOrder < 2; submissionOrder++)
            {
                CampaignSimulation simulation = CreateCampaign(
                    [FirstParent, SecondParent],
                    [seed]);
                CampaignCommand earlierCommand = BirthCommand(
                    simulation,
                    seed,
                    earlier,
                    earlierName);
                CampaignCommand laterCommand = BirthCommand(
                    simulation,
                    seed,
                    later,
                    laterName);
                CampaignCommand[] submissions = submissionOrder == 0
                    ? [earlierCommand, laterCommand]
                    : [laterCommand, earlierCommand];
                foreach (CampaignCommand command in submissions)
                {
                    Assert.True(simulation.Submit(command).IsValid);
                }

                IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

                Assert.Equal(2, events.Count);
                CharacterFamilyActionResolvedEventPayload winner = Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(events[0].Payload);
                Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
                Assert.Equal(earlier, events[0].CausalId);
                Assert.Equal(later, events[1].CausalId);
                Assert.Equal(
                    earlierName,
                    Assert.IsType<PregnancyBirthResolvedOutcome>(winner.Outcome)
                        .Birth.ChildDefinition.NameKey);
                Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
            }
        }
    }

    [Fact]
    public void IndependentBirthsCommuteWithSharedFamilyAndHousehold()
    {
        BirthSeed first = new("commute-first", FirstParent, SecondParent);
        BirthSeed second = new("commute-second", ThirdParent, FourthParent);
        CampaignSimulation forward = CreateCampaign(
            [FirstParent, SecondParent, ThirdParent, FourthParent],
            [first, second]);
        CampaignSimulation reverse = CreateCampaign(
            [FirstParent, SecondParent, ThirdParent, FourthParent],
            [first, second]);
        Assert.True(forward.Submit(BirthCommand(forward, first, "commute-first")).IsValid);
        Assert.True(forward.Submit(BirthCommand(forward, second, "commute-second")).IsValid);
        Assert.True(reverse.Submit(BirthCommand(reverse, second, "commute-second")).IsValid);
        Assert.True(reverse.Submit(BirthCommand(reverse, first, "commute-first")).IsValid);

        IReadOnlyList<CampaignEvent> forwardEvents = forward.ResolveTurn();
        IReadOnlyList<CampaignEvent> reverseEvents = reverse.ResolveTurn();

        Assert.Equal(2, forwardEvents.Count);
        Assert.Equal(Serialize(forwardEvents), Serialize(reverseEvents));
        Assert.Equal(
            SimulationChecksum.Compute(forward.World.CaptureSnapshot()),
            SimulationChecksum.Compute(reverse.World.CaptureSnapshot()));
        FamilyState family = Assert.Single(forward.World.CaptureSnapshot().Characters.FamilyStates);
        HouseholdState household = Assert.Single(
            forward.World.CaptureSnapshot().Characters.HouseholdStates);
        Assert.Contains(CharacterBirthIds.DeriveChildId(first.PregnancyId), family.MemberIds);
        Assert.Contains(CharacterBirthIds.DeriveChildId(second.PregnancyId), family.MemberIds);
        Assert.Contains(CharacterBirthIds.DeriveChildId(first.PregnancyId), household.MemberIds);
        Assert.Contains(CharacterBirthIds.DeriveChildId(second.PregnancyId), household.MemberIds);
    }

    [Fact]
    public void EarlierHouseholdChangeMakesStaleBirthPlacementCancelAtomically()
    {
        BirthSeed seed = new("stale-placement", FirstParent, SecondParent);
        CampaignSimulation seeded = CreateCampaign(
            [FirstParent, SecondParent, ThirdParent],
            [seed]);
        EntityId secondHouseholdId = new("household:test/birth-second");
        WorldSnapshot snapshot = seeded.World.CaptureSnapshot();
        CharacterWorldSnapshot characters = snapshot.Characters with
        {
            HouseholdDefinitions = snapshot.Characters.HouseholdDefinitions.Append(
                new HouseholdDefinition(
                    CharacterContractVersions.Definition,
                    secondHouseholdId,
                    new EntityId("loc:household/test_birth_second"))).ToArray(),
            HouseholdStates =
            [
                new HouseholdState(
                    CharacterContractVersions.State,
                    HouseholdId,
                    FirstParent,
                    [FirstParent]),
                new HouseholdState(
                    CharacterContractVersions.State,
                    secondHouseholdId,
                    ThirdParent,
                    new[] { SecondParent, ThirdParent }.Order().ToArray()),
            ],
        };
        CampaignSimulation simulation = new(WorldState.Restore(snapshot with
        {
            Characters = characters.Canonicalize(),
        }));
        CampaignCommand expulsion = CampaignCommand.Create(
            new EntityId("command:test/birth-stale-expulsion"),
            ThirdParent,
            DueDate,
            new HouseholdDecisionCommandPayload(
                new ExpelHouseholdMemberAction(
                    secondHouseholdId,
                    SecondParent)),
            ResolutionPhase.Commands,
            priority: -1);
        CampaignCommand birth = CampaignCommand.Create(
            new EntityId("command:test/birth-stale-placement"),
            CharacterFamilySystem.AuthoritativeActorId,
            DueDate,
            new CharacterFamilyActionCommandPayload(
                new ResolvePregnancyBirthAction(
                    seed.PregnancyId,
                    new GeneratedNewbornSpecification(
                        CharacterBirthContractVersions.NewbornSpecification,
                        new EntityId("loc:test/birth_stale_placement"),
                        CultureId,
                        FamilyId,
                        secondHouseholdId,
                        [TraitId]))),
            ResolutionPhase.Commands);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        Assert.True(simulation.Submit(expulsion).IsValid);
        Assert.True(simulation.Submit(birth).IsValid);

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.Equal(2, events.Count);
        Assert.IsType<HouseholdDecisionResolvedEventPayload>(events[0].Payload);
        Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            SecondParent,
            out AuthoritativeCharacterProfile? secondParent));
        Assert.Null(secondParent.HouseholdId);
        Assert.Single(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.False(simulation.World.Characters.TryGetCharacterProfile(
            CharacterBirthIds.DeriveChildId(seed.PregnancyId),
            out _));
        Assert.Equal(
            Serialize(before.RandomStreams),
            Serialize(simulation.World.CaptureSnapshot().RandomStreams));
    }

    [Fact]
    public void TamperingAndReplayRollBackBothWorlds()
    {
        BirthSeed seed = new("rollback", FirstParent, SecondParent);
        CampaignSimulation simulation = CreateCampaign(
            [FirstParent, SecondParent],
            [seed]);
        CampaignCommand command = BirthCommand(simulation, seed, "rollback");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(DueDate, command.CommandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            CharacterFamilySystem.AuthoritativeActorId,
            Assert.IsType<CharacterFamilyActionCommandPayload>(command.Payload),
            DueDate,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        PregnancyBirthResolvedOutcome outcome = Assert.IsType<
            PregnancyBirthResolvedOutcome>(plan.ResolvedPayload.Outcome);
        CampaignEvent exact = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            DueDate,
            ResolutionPhase.Commands,
            command.Priority,
            WorldState.GetCharacterFamilyActionAffectedIds(plan.ResolvedPayload),
            plan.ResolvedPayload);
        CharacterFamilyActionResolvedEventPayload tamperedPayload = plan.ResolvedPayload with
        {
            Outcome = outcome with
            {
                Birth = outcome.Birth with
                {
                    ChildDefinition = outcome.Birth.ChildDefinition with
                    {
                        BirthDate = outcome.Birth.ChildDefinition.BirthDate.AddDays(1),
                    },
                },
            },
        };
        CampaignEvent tamperedPayloadEvent = exact with
        {
            AffectedIds = WorldState.GetCharacterFamilyActionAffectedIds(tamperedPayload),
            Payload = tamperedPayload,
        };
        CampaignEvent tamperedAffectedIds = exact with
        {
            AffectedIds = exact.AffectedIds.Skip(1).ToArray(),
        };
        CampaignEvent tamperedCausalId = exact with
        {
            CausalId = new EntityId("command:test/birth-forged-causal"),
        };
        CampaignEvent tamperedEventId = exact with
        {
            EventId = new EntityId("event:test/birth-forged-event"),
        };
        string before = Serialize(simulation.World.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedPayloadEvent));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedAffectedIds));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedCausalId));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Throws<SimulationValidationException>(() =>
            simulation.World.Apply(tamperedEventId));
        Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));

        simulation.World.Apply(exact);
        string after = Serialize(simulation.World.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(exact));
        Assert.Equal(after, Serialize(simulation.World.CaptureSnapshot()));
        Assert.Empty(simulation.World.CharacterPregnancies.ActivePregnancies);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            outcome.Birth.ChildDefinition.Id,
            out _));
    }

    [Fact]
    public void PendingAndResolvedBirthRoundTripThroughJsonAndGzip()
    {
        BirthSeed seed = new("round-trip", FirstParent, SecondParent);
        CampaignSimulation original = CreateCampaign(
            [FirstParent, SecondParent],
            [seed]);
        Assert.True(original.Submit(BirthCommand(original, seed, "round-trip")).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-birth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SaveStore store = new();
            string pendingPath = Path.Combine(directory, "birth-pending.save.gz");
            store.SaveAtomic(pendingPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(pendingPath);
            CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));

            IReadOnlyList<CampaignEvent> expected = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> actual = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
            Assert.IsType<ResolvePregnancyBirthAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(Assert.Single(
                    pending.Snapshot.PendingCommands).Payload).Action);
            Assert.Equal(Serialize(expected), Serialize(actual));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));

            SaveEnvelope resolved = SaveEnvelope.Create("test", [], original);
            string resolvedPath = Path.Combine(directory, "birth-resolved.save.gz");
            store.SaveAtomic(resolvedPath, resolved);
            SaveEnvelope gzipRoundTrip = store.Load(resolvedPath);
            PregnancyBirthResolvedOutcome gzipOutcome = Assert.IsType<
                PregnancyBirthResolvedOutcome>(Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(Assert.Single(
                        gzipRoundTrip.DiagnosticEvents).Payload).Outcome);
            Assert.Empty(gzipRoundTrip.Snapshot.CharacterPregnancies.ActivePregnancies);
            Assert.Contains(
                gzipRoundTrip.Snapshot.Characters.CharacterDefinitions,
                definition => definition.Id == gzipOutcome.Birth.ChildDefinition.Id);
            Assert.Equal(
                SimulationChecksum.Compute(gzipRoundTrip.Snapshot).Value,
                gzipRoundTrip.Checksum);

            SaveEnvelope jsonRoundTrip = JsonSerializer.Deserialize<SaveEnvelope>(
                Serialize(resolved),
                SimulationJson.CreateOptions())!;
            Assert.IsType<PregnancyBirthResolvedOutcome>(Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(Assert.Single(
                    jsonRoundTrip.DiagnosticEvents).Payload).Outcome);
            Assert.Equal(resolved.Checksum, jsonRoundTrip.Checksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EightHundredAdultsAndTwoHundredBirthsRecordBoundedMeasurements()
    {
        EntityId[] adults = Enumerable.Range(0, 800)
            .Select(index => new EntityId($"character:birth-performance/{index:D4}"))
            .ToArray();
        BirthSeed[] births = Enumerable.Range(0, 200)
            .Select(index => new BirthSeed(
                $"performance-{index:D3}",
                adults[index * 2],
                adults[(index * 2) + 1]))
            .ToArray();
        CampaignSimulation simulation = CreateCampaign(adults, births);
        Stopwatch workflow = Stopwatch.StartNew();
        foreach (BirthSeed seed in births.Reverse())
        {
            Assert.True(simulation.Submit(BirthCommand(
                simulation,
                seed,
                $"performance-{seed.Suffix}")).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
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

        Assert.Equal(200, events.Count);
        Assert.Equal(1_000, snapshot.Characters.CharacterDefinitions.Count);
        Assert.Empty(snapshot.CharacterPregnancies.ActivePregnancies);
        Assert.False(string.IsNullOrWhiteSpace(checksum.Value));
        Assert.NotEmpty(json);
        Assert.True(compressed.Length > 0);
        output.WriteLine(
            $"birth_raw adults=800 births=200 workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} "
            + $"checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateCampaign(
        IReadOnlyList<EntityId> characterIds,
        IReadOnlyList<BirthSeed> birthSeeds)
    {
        CharacterIdentityDefinition trait = new(
            CharacterContractVersions.Definition,
            TraitId,
            CharacterIdentityKind.Trait,
            new EntityId("loc:trait/test_birth"));
        CharacterDefinition[] definitions = characterIds
            .Order()
            .Select(id =>
            {
                EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
                return new CharacterDefinition(
                    CharacterContractVersions.Definition,
                    id,
                    nameKey,
                    new CampaignDate(170, 1, 1),
                    [],
                    [],
                    [TraitId],
                    [],
                    [],
                    new StructuredCharacterName(nameKey, null),
                    CharacterContentOrigin.LegacyUnknown(id),
                    CultureId,
                    null,
                    []);
            })
            .ToArray();
        CharacterState[] states = characterIds
            .Order()
            .Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                [],
                [],
                CharacterConditionState.Default))
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [trait],
            definitions,
            [new FamilyDefinition(
                CharacterContractVersions.Definition,
                FamilyId,
                new EntityId("loc:family/test_birth"))],
            [new HouseholdDefinition(
                CharacterContractVersions.Definition,
                HouseholdId,
                new EntityId("loc:household/test_birth"))],
            states,
            [new FamilyState(
                CharacterContractVersions.State,
                FamilyId,
                characterIds.Order().ToArray())],
            [new HouseholdState(
                CharacterContractVersions.State,
                HouseholdId,
                characterIds.Order().First(),
                characterIds.Order().ToArray())]);
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
        MarriageProposalState[] proposals = birthSeeds
            .Select(Proposal)
            .OrderBy(item => item.ProposalId)
            .ToArray();
        Dictionary<EntityId, MarriageProposalState> proposalById = proposals
            .ToDictionary(item => item.ProposalId);
        MarriageUnionState[] unions = birthSeeds
            .Select(seed => Union(seed, proposalById[seed.ProposalId]))
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
        CharacterPregnancyState[] pregnancies = birthSeeds
            .Select(seed => new CharacterPregnancyState(
                CharacterPregnancyContractVersions.State,
                seed.PregnancyId,
                seed.FirstParent,
                seed.SecondParent,
                seed.UnionId,
                PregnancyStart,
                DueDate,
                0,
                seed.RegistrationCommandId,
                seed.RegistrationEventId))
            .OrderBy(item => item.PregnancyId)
            .ToArray();
        return new CampaignSimulation(WorldState.Create(
            DueDate,
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
            new CharacterPregnancyWorldSnapshot(
                CharacterPregnancyContractVersions.Snapshot,
                pregnancies)));
    }

    private static MarriageProposalState Proposal(BirthSeed seed) => new(
        CharacterMarriageContractVersions.State,
        seed.ProposalId,
        MarriageProposalKind.LegalUnion,
        MarriageBasis.Political,
        MarriageUnionForm.PrincipalSpouse,
        MarriageConsentKind.PoliticalArrangement,
        seed.FirstParent.CompareTo(seed.SecondParent) < 0
            ? seed.FirstParent
            : seed.SecondParent,
        seed.FirstParent.CompareTo(seed.SecondParent) < 0
            ? seed.SecondParent
            : seed.FirstParent,
        null,
        PracticeId,
        PregnancyStart.AddDays(-2),
        0,
        new EntityId($"command:test/birth-proposal-{seed.Suffix}"),
        MarriageProposalStatus.Accepted,
        PregnancyStart.AddDays(-1),
        0,
        new EntityId($"command:test/birth-accept-{seed.Suffix}"));

    private static MarriageUnionState Union(
        BirthSeed seed,
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

    private static CampaignCommand BirthCommand(
        CampaignSimulation simulation,
        BirthSeed seed,
        string suffix) => BirthCommand(
        simulation,
        seed,
        new EntityId($"command:test/birth-{suffix}"),
        new EntityId($"loc:test/birth_{suffix.Replace('-', '_')}"));

    private static CampaignCommand BirthCommand(
        CampaignSimulation simulation,
        BirthSeed seed,
        EntityId commandId,
        EntityId nameKey) => CampaignCommand.Create(
        commandId,
        CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new ResolvePregnancyBirthAction(
                seed.PregnancyId,
                new GeneratedNewbornSpecification(
                    CharacterBirthContractVersions.NewbornSpecification,
                    nameKey,
                    CultureId,
                    FamilyId,
                    HouseholdId,
                    [TraitId]))),
        ResolutionPhase.Commands);

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());

    private sealed record BirthSeed(
        string Suffix,
        EntityId FirstParent,
        EntityId SecondParent)
    {
        public EntityId UnionId { get; } = new($"marriage_union:test/birth/{Suffix}");

        public EntityId ProposalId { get; } = new($"marriage_proposal:test/birth/{Suffix}");

        public EntityId RegistrationCommandId { get; } = new(
            $"command:test/birth-registration-{Suffix}");

        public EntityId RegistrationEventId { get; } = CharacterFamilyIds.DeriveActionEventId(
            PregnancyStart,
            new EntityId($"command:test/birth-registration-{Suffix}"));

        public EntityId PregnancyId { get; } = CharacterPregnancyIds.DerivePregnancyId(
            CharacterFamilyIds.DeriveActionEventId(
                PregnancyStart,
                new EntityId($"command:test/birth-registration-{Suffix}")),
            FirstParent,
            SecondParent,
            new EntityId($"marriage_union:test/birth/{Suffix}"));
    }
}
