using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterEducationCampaignTests
{
    private static readonly CampaignDate Date = new(200, 6, 1);
    private static readonly EntityId Ward = new("character:test/education_campaign_ward");
    private static readonly EntityId Teacher = new("character:test/education_campaign_teacher");
    private static readonly EntityId OtherTeacher = new("character:test/education_campaign_other_teacher");
    private static readonly EntityId AbilityA = new("ability:test/education_campaign_a");
    private static readonly EntityId AbilityB = new("ability:test/education_campaign_b");
    private readonly ITestOutputHelper output;

    public CharacterEducationCampaignTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void RegisteredEducationActionEnforcesAuthorityAndPhaseAndMutatesOnlyCharacterState()
    {
        CampaignSimulation simulation = CreateCampaign();
        CharacterGuardianshipState guardianship = ActiveGuardianship();
        Assert.False(simulation.Submit(EducationCommand(
            simulation,
            "unauthorized",
            AbilityA,
            issuingActor: Teacher)).IsValid);
        Assert.False(simulation.Submit(EducationCommand(
            simulation,
            "wrong-phase",
            AbilityA,
            phase: ResolutionPhase.Systems)).IsValid);
        WorldSnapshot before = simulation.World.CaptureSnapshot();
        CampaignCommand command = EducationCommand(simulation, "success", AbilityA);

        Assert.True(simulation.Submit(command).IsValid);
        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());
        CharacterFamilyActionResolvedEventPayload payload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(campaignEvent.Payload);
        PrimaryGuardianEducationCompletedOutcome outcome = Assert.IsType<
            PrimaryGuardianEducationCompletedOutcome>(payload.Outcome);
        CharacterEducationAttainment attainment = outcome.Attainment;

        Assert.Equal(
            CharacterFamilyIds.DeriveActionEventId(Date, command.CommandId),
            campaignEvent.EventId);
        Assert.Equal(
            CharacterEducationIds.DeriveAttainmentId(
                campaignEvent.EventId,
                Ward,
                Teacher,
                AbilityA),
            attainment.AttainmentId);
        Assert.Equal(command.CommandId, attainment.SourceCommandId);
        Assert.Equal(campaignEvent.EventId, attainment.SourceEventId);
        Assert.Equal(guardianship.GuardianshipId, attainment.PrimaryGuardianshipId);
        Assert.Equal(
            new[]
            {
                CharacterFamilySystem.AuthoritativeActorId,
                attainment.AttainmentId,
                Ward,
                Teacher,
                guardianship.GuardianshipId,
                AbilityA,
            }.Distinct().Order(),
            campaignEvent.AffectedIds);
        Assert.Equal(
            WorldState.GetCharacterFamilyActionAffectedIds(payload),
            campaignEvent.AffectedIds);
        Assert.True(simulation.World.Characters.TryGetCharacterProfile(
            Ward,
            out AuthoritativeCharacterProfile? ward));
        Assert.Equal([AbilityA], ward.AbilityIds);
        Assert.Equal(attainment, Assert.Single(ward.EducationAttainments));

        WorldSnapshot after = simulation.World.CaptureSnapshot();
        Assert.NotEqual(Serialize(before.Characters), Serialize(after.Characters));
        CharacterState originalWardState = before.Characters.CharacterStates
            .Single(item => item.CharacterId == Ward);
        CharacterWorldSnapshot normalizedCharacters = after.Characters with
        {
            CharacterStates = after.Characters.CharacterStates.Select(state =>
                state.CharacterId == Ward
                    ? state with
                    {
                        EducationAttainments = originalWardState.EducationAttainments,
                    }
                    : state).ToArray(),
        };
        Assert.Equal(Serialize(before.Characters), Serialize(normalizedCharacters));
        Assert.Equal(Serialize(before.CharacterGuardianships), Serialize(after.CharacterGuardianships));
        AssertEqualUnrelatedSubsystems(before, after);
    }

    [Fact]
    public void EducationActionAndOutcomeUseClosedNestedJsonContracts()
    {
        CampaignSimulation simulation = CreateCampaign();
        CampaignCommand command = EducationCommand(simulation, "json-contract", AbilityA);
        (CampaignEvent campaignEvent, _) = PlanEvent(simulation, command);
        JsonObject commandJson = JsonNode.Parse(Serialize(command))!.AsObject();
        JsonObject actionJson = commandJson["payload"]!["action"]!.AsObject();
        JsonObject eventJson = JsonNode.Parse(Serialize(campaignEvent))!.AsObject();
        JsonObject outcomeJson = eventJson["payload"]!["outcome"]!.AsObject();
        JsonObject attainmentJson = outcomeJson["attainment"]!.AsObject();

        Assert.Equal(
            "complete_primary_guardian_education.v1",
            actionJson["$type"]!.GetValue<string>());
        Assert.Equal(
            new[] { "$type", "abilityId", "expectedPrimaryGuardianshipId", "wardCharacterId" },
            actionJson.Select(item => item.Key).Order());
        Assert.Equal(
            "primary_guardian_education_completed.v1",
            outcomeJson["$type"]!.GetValue<string>());
        Assert.Equal(new[] { "$type", "attainment" }, outcomeJson.Select(item => item.Key).Order());
        Assert.Equal(
            new[]
            {
                "abilityId",
                "attainmentId",
                "contractVersion",
                "primaryGuardianshipId",
                "resolutionDate",
                "resolutionTurnIndex",
                "sourceCommandId",
                "sourceEventId",
                "teacherCharacterId",
                "wardCharacterId",
            }.Order(),
            attainmentJson.Select(item => item.Key).Order());
        Assert.IsType<CompletePrimaryGuardianEducationAction>(Assert.IsType<
            CharacterFamilyActionCommandPayload>(JsonSerializer.Deserialize<CampaignCommand>(
                Serialize(command),
                SimulationJson.CreateOptions())!.Payload).Action);
        Assert.IsType<PrimaryGuardianEducationCompletedOutcome>(Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(JsonSerializer.Deserialize<CampaignEvent>(
                Serialize(campaignEvent),
                SimulationJson.CreateOptions())!.Payload).Outcome);
        Assert.Equal(
            new EntityId(
                "character_education_attainment:sha256/c46b962cdb29663ec340d4647b46bba9dd21ead7bab87313319e2e22c3c50691"),
            CharacterEducationIds.DeriveAttainmentId(
                new EntityId("event:test/education-golden"),
                Ward,
                Teacher,
                AbilityA));
    }

    [Fact]
    public void SameAbilityRaceUsesEventIdentityRegardlessOfSubmissionOrder()
    {
        (EntityId earlier, EntityId later) = OrderedCommandIds("race-first", "race-second");
        foreach (bool reverseSubmission in new[] { false, true })
        {
            CampaignSimulation simulation = CreateCampaign();
            CampaignCommand first = EducationCommand(simulation, earlier, AbilityA);
            CampaignCommand second = EducationCommand(simulation, later, AbilityA);
            CampaignCommand[] submitted = reverseSubmission ? [second, first] : [first, second];
            Assert.All(submitted, command => Assert.True(simulation.Submit(command).IsValid));

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            Assert.Equal(2, events.Count);
            PrimaryGuardianEducationCompletedOutcome outcome = Assert.IsType<
                PrimaryGuardianEducationCompletedOutcome>(Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(events[0].Payload).Outcome);
            Assert.Equal(earlier, outcome.Attainment.SourceCommandId);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
            Assert.True(simulation.World.Characters.TryGetCharacterProfile(
                Ward,
                out AuthoritativeCharacterProfile? ward));
            Assert.Single(ward.EducationAttainments);
        }
    }

    [Fact]
    public void SameAbilityRaceUsesPriorityBeforeEventIdentity()
    {
        (EntityId earlier, EntityId later) = OrderedCommandIds("priority-first", "priority-second");
        foreach (bool reverseSubmission in new[] { false, true })
        {
            CampaignSimulation simulation = CreateCampaign();
            CampaignCommand eventEarlier = EducationCommand(simulation, earlier, AbilityA);
            CampaignCommand priorityEarlier = EducationCommand(
                simulation,
                later,
                AbilityA,
                priority: -1);
            CampaignCommand[] submitted = reverseSubmission
                ? [eventEarlier, priorityEarlier]
                : [priorityEarlier, eventEarlier];
            Assert.All(submitted, command => Assert.True(simulation.Submit(command).IsValid));

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            PrimaryGuardianEducationCompletedOutcome outcome = Assert.IsType<
                PrimaryGuardianEducationCompletedOutcome>(Assert.IsType<
                    CharacterFamilyActionResolvedEventPayload>(events[0].Payload).Outcome);
            Assert.Equal(later, events[0].CausalId);
            Assert.Equal(later, outcome.Attainment.SourceCommandId);
            Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        }
    }

    [Fact]
    public void IndependentAbilitiesCommute()
    {
        List<string> finalSnapshots = [];
        List<SimulationChecksum> finalChecksums = [];
        foreach (bool reverseSubmission in new[] { false, true })
        {
            CampaignSimulation simulation = CreateCampaign();
            CampaignCommand abilityA = EducationCommand(simulation, "commute-a", AbilityA);
            CampaignCommand abilityB = EducationCommand(simulation, "commute-b", AbilityB);
            CampaignCommand[] submitted = reverseSubmission
                ? [abilityB, abilityA]
                : [abilityA, abilityB];
            Assert.All(submitted, command => Assert.True(simulation.Submit(command).IsValid));

            IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

            Assert.Equal(2, events.Count);
            Assert.All(events, item => Assert.IsType<
                CharacterFamilyActionResolvedEventPayload>(item.Payload));
            Assert.True(simulation.World.Characters.TryGetCharacterProfile(
                Ward,
                out AuthoritativeCharacterProfile? ward));
            Assert.Equal(new[] { AbilityA, AbilityB }.Order(), ward.AbilityIds);
            Assert.Equal(2, ward.EducationAttainments.Count);
            WorldSnapshot final = simulation.World.CaptureSnapshot();
            finalSnapshots.Add(Serialize(final));
            finalChecksums.Add(SimulationChecksum.Compute(final));
        }

        Assert.Equal(finalSnapshots[0], finalSnapshots[1]);
        Assert.Equal(finalChecksums[0], finalChecksums[1]);
    }

    [Fact]
    public void GuardianshipEndAndEducationReplanInPriorityOrder()
    {
        CampaignSimulation endingFirst = CreateCampaign();
        Assert.True(endingFirst.Submit(EndGuardianshipCommand(
            endingFirst,
            "end-first",
            priority: -1)).IsValid);
        Assert.True(endingFirst.Submit(EducationCommand(
            endingFirst,
            "education-after-end",
            AbilityA)).IsValid);

        IReadOnlyList<CampaignEvent> endingFirstEvents = endingFirst.ResolveTurn();

        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(endingFirstEvents[0].Payload);
        Assert.IsType<CommandCancelledEventPayload>(endingFirstEvents[1].Payload);
        Assert.True(endingFirst.World.Characters.TryGetCharacterProfile(
            Ward,
            out AuthoritativeCharacterProfile? noEducation));
        Assert.Empty(noEducation.EducationAttainments);

        CampaignSimulation educationFirst = CreateCampaign();
        Assert.True(educationFirst.Submit(EducationCommand(
            educationFirst,
            "education-first",
            AbilityA,
            priority: -1)).IsValid);
        Assert.True(educationFirst.Submit(EndGuardianshipCommand(
            educationFirst,
            "end-after-education")).IsValid);

        IReadOnlyList<CampaignEvent> educationFirstEvents = educationFirst.ResolveTurn();

        Assert.All(educationFirstEvents, item => Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(item.Payload));
        Assert.True(educationFirst.World.Characters.TryGetCharacterProfile(
            Ward,
            out AuthoritativeCharacterProfile? educated));
        Assert.Single(educated.EducationAttainments);
        Assert.False(educationFirst.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
        _ = WorldState.Restore(educationFirst.World.CaptureSnapshot());
    }

    [Fact]
    public void GuardianshipReplacementAndEducationReplanInPriorityOrder()
    {
        CampaignSimulation replacementFirst = CreateCampaign();
        Assert.True(replacementFirst.Submit(ReplaceGuardianshipCommand(
            replacementFirst,
            "replace-first",
            priority: -1)).IsValid);
        Assert.True(replacementFirst.Submit(EducationCommand(
            replacementFirst,
            "education-after-replace",
            AbilityA)).IsValid);

        IReadOnlyList<CampaignEvent> replacementFirstEvents = replacementFirst.ResolveTurn();

        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(replacementFirstEvents[0].Payload);
        Assert.IsType<CommandCancelledEventPayload>(replacementFirstEvents[1].Payload);
        Assert.Empty(replacementFirst.World.Characters.Profiles
            .Single(item => item.CharacterId == Ward)
            .EducationAttainments);

        CampaignSimulation educationFirst = CreateCampaign();
        Assert.True(educationFirst.Submit(EducationCommand(
            educationFirst,
            "education-before-replace",
            AbilityA,
            priority: -1)).IsValid);
        Assert.True(educationFirst.Submit(ReplaceGuardianshipCommand(
            educationFirst,
            "replace-after-education")).IsValid);

        Assert.All(educationFirst.ResolveTurn(), item => Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(item.Payload));
        Assert.Single(educationFirst.World.Characters.Profiles
            .Single(item => item.CharacterId == Ward)
            .EducationAttainments);
        Assert.True(educationFirst.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(
                Ward,
                out CharacterGuardianshipState? replacement));
        Assert.Equal(OtherTeacher, replacement.GuardianCharacterId);
        _ = WorldState.Restore(educationFirst.World.CaptureSnapshot());
    }

    [Fact]
    public void WardConditionAndEducationReplanInPriorityOrder()
    {
        CampaignSimulation conditionFirst = CreateCampaign();
        Assert.True(conditionFirst.Submit(IncapacitateWardCommand(
            conditionFirst,
            "condition-first",
            priority: -1)).IsValid);
        Assert.True(conditionFirst.Submit(EducationCommand(
            conditionFirst,
            "education-after-condition",
            AbilityA)).IsValid);

        IReadOnlyList<CampaignEvent> conditionFirstEvents = conditionFirst.ResolveTurn();

        Assert.IsType<CharacterConditionActionResolvedEventPayload>(conditionFirstEvents[0].Payload);
        Assert.IsType<CommandCancelledEventPayload>(conditionFirstEvents[1].Payload);
        Assert.Empty(conditionFirst.World.Characters.Profiles
            .Single(item => item.CharacterId == Ward)
            .EducationAttainments);

        CampaignSimulation educationFirst = CreateCampaign();
        Assert.True(educationFirst.Submit(EducationCommand(
            educationFirst,
            "education-before-condition",
            AbilityA,
            priority: -1)).IsValid);
        Assert.True(educationFirst.Submit(IncapacitateWardCommand(
            educationFirst,
            "condition-after-education")).IsValid);

        IReadOnlyList<CampaignEvent> educationFirstEvents = educationFirst.ResolveTurn();

        Assert.IsType<CharacterFamilyActionResolvedEventPayload>(educationFirstEvents[0].Payload);
        Assert.IsType<CharacterConditionActionResolvedEventPayload>(educationFirstEvents[1].Payload);
        AuthoritativeCharacterProfile ward = educationFirst.World.Characters.Profiles
            .Single(item => item.CharacterId == Ward);
        Assert.Single(ward.EducationAttainments);
        Assert.True(ward.Condition.IsIncapacitated);
    }

    [Fact]
    public void ExactEighteenthBirthdayRejectsEducationBeforeComingOfAgeSystemsPhase()
    {
        CampaignSimulation simulation = CreateCampaign(
            Date,
            wardBirthDate: new CampaignDate(182, 6, 1));
        CampaignCommand birthdayCommand = EducationCommand(
            simulation,
            "birthday",
            AbilityA,
            issuedDate: Date);

        CommandValidationResult validation = simulation.Submit(birthdayCommand);

        Assert.False(validation.IsValid);
        Assert.Empty(simulation.World.Characters.Profiles
            .Single(item => item.CharacterId == Ward)
            .EducationAttainments);
        CampaignEvent comingOfAge = Assert.Single(simulation.ResolveTurn());
        CharacterCameOfAgeEventPayload payload = Assert.IsType<
            CharacterCameOfAgeEventPayload>(comingOfAge.Payload);
        Assert.Equal(Ward, payload.CharacterId);
        Assert.Equal(
            CharacterGuardianshipEndReason.WardCameOfAge,
            Assert.IsType<CharacterGuardianshipState>(payload.EndedPrimaryGuardianship).EndReason);
        Assert.False(simulation.World.CharacterGuardianships
            .TryGetActivePrimaryGuardianshipForWard(Ward, out _));
    }

    [Fact]
    public void TamperedEducationEventsAndReplayRollBackAtomically()
    {
        CampaignSimulation simulation = CreateCampaign();
        CampaignCommand command = EducationCommand(simulation, "tamper", AbilityA);
        (CampaignEvent valid, PrimaryGuardianEducationCompletedOutcome outcome) =
            PlanEvent(simulation, command);
        string before = Serialize(simulation.World.CaptureSnapshot());
        CharacterEducationAttainment forged = outcome.Attainment with
        {
            SourceEventId = new EntityId("event:test/forged-education-source"),
        };
        CharacterFamilyActionResolvedEventPayload forgedPayload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(valid.Payload) with
        {
            Outcome = outcome with { Attainment = forged },
        };
        CharacterEducationAttainment forgedSourceCommand = outcome.Attainment with
        {
            SourceCommandId = new EntityId("command:test/forged-education-source"),
        };
        CharacterFamilyActionResolvedEventPayload forgedSourceCommandPayload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(valid.Payload) with
        {
            Outcome = outcome with { Attainment = forgedSourceCommand },
        };
        CharacterFamilyActionResolvedEventPayload mismatchedActionPayload = Assert.IsType<
            CharacterFamilyActionResolvedEventPayload>(valid.Payload) with
        {
            Action = new CompletePrimaryGuardianEducationAction(
                Ward,
                ActiveGuardianship().GuardianshipId,
                AbilityB),
        };
        CampaignEvent[] invalid =
        [
            valid with
            {
                Payload = forgedPayload,
                AffectedIds = WorldState.GetCharacterFamilyActionAffectedIds(forgedPayload),
            },
            valid with
            {
                Payload = forgedSourceCommandPayload,
                AffectedIds = WorldState.GetCharacterFamilyActionAffectedIds(
                    forgedSourceCommandPayload),
            },
            valid with
            {
                Payload = mismatchedActionPayload,
                AffectedIds = valid.AffectedIds,
            },
            valid with { AffectedIds = valid.AffectedIds.Skip(1).ToArray() },
            valid with { CausalId = new EntityId("command:test/forged-causal") },
            valid with { EventId = new EntityId("event:test/forged-education-event") },
        ];
        foreach (CampaignEvent item in invalid)
        {
            Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(item));
            Assert.Equal(before, Serialize(simulation.World.CaptureSnapshot()));
        }

        simulation.World.Apply(valid);
        string applied = Serialize(simulation.World.CaptureSnapshot());
        Assert.Throws<SimulationValidationException>(() => simulation.World.Apply(valid));
        Assert.Equal(applied, Serialize(simulation.World.CaptureSnapshot()));
    }

    [Fact]
    public void PendingAndResolvedEducationRoundTripThroughCurrentSaveAndReplay()
    {
        CampaignSimulation original = CreateCampaign();
        CampaignCommand command = EducationCommand(original, "save-replay", AbilityA);
        Assert.True(original.Submit(command).IsValid);
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"three-kingdom-education-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            SaveStore store = new();
            string pendingPath = Path.Combine(directory, "pending.save.gz");
            store.SaveAtomic(pendingPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope pending = store.Load(pendingPath);
            CampaignSimulation replay = new(WorldState.Restore(pending.Snapshot));

            IReadOnlyList<CampaignEvent> originalEvents = original.ResolveTurn();
            IReadOnlyList<CampaignEvent> replayEvents = replay.ResolveTurn();

            Assert.Equal(SaveEnvelope.CurrentSchemaVersion, pending.SchemaVersion);
            Assert.IsType<CompletePrimaryGuardianEducationAction>(Assert.IsType<
                CharacterFamilyActionCommandPayload>(Assert.Single(
                    pending.Snapshot.PendingCommands).Payload).Action);
            Assert.Equal(Serialize(originalEvents), Serialize(replayEvents));
            Assert.Equal(
                SimulationChecksum.Compute(original.World.CaptureSnapshot()),
                SimulationChecksum.Compute(replay.World.CaptureSnapshot()));

            string resolvedPath = Path.Combine(directory, "resolved.save.gz");
            store.SaveAtomic(resolvedPath, SaveEnvelope.Create("test", [], original));
            SaveEnvelope resolved = store.Load(resolvedPath);
            WorldState restored = WorldState.Restore(resolved.Snapshot);
            Assert.True(restored.Characters.TryGetCharacterProfile(
                Ward,
                out AuthoritativeCharacterProfile? restoredWard));
            Assert.Single(restoredWard.EducationAttainments);
            Assert.Contains(resolved.DiagnosticCommands, item =>
                item.Payload is CharacterFamilyActionCommandPayload family
                && family.Action is CompletePrimaryGuardianEducationAction);
            Assert.Contains(resolved.DiagnosticEvents, item =>
                item.Payload is CharacterFamilyActionResolvedEventPayload family
                && family.Outcome is PrimaryGuardianEducationCompletedOutcome);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoreRejectsAttainmentWithoutItsRetainedGuardianshipSource()
    {
        CampaignSimulation simulation = CreateCampaign();
        Assert.True(simulation.Submit(EducationCommand(
            simulation,
            "restore-cross-reference",
            AbilityA)).IsValid);
        Assert.Single(simulation.ResolveTurn());
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot() with
        {
            CharacterGuardianships = CharacterGuardianshipWorldSnapshot.Empty,
        };

        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(snapshot));
    }

    [Fact]
    public void ThousandCharacterEducationFixtureRecordsRawWorkflowQueryAndPersistenceMeasurements()
    {
        CampaignSimulation simulation = CreateLargeCampaign(1_000);
        Stopwatch workflow = Stopwatch.StartNew();
        Assert.True(simulation.Submit(EducationCommand(
            simulation,
            "performance",
            AbilityA)).IsValid);
        Assert.Single(simulation.ResolveTurn());
        workflow.Stop();

        Stopwatch query = Stopwatch.StartNew();
        AuthoritativeCharacterProfile[] profiles = simulation.World.Characters.Profiles.ToArray();
        AuthoritativeCharacterProfile ward = profiles.Single(item => item.CharacterId == Ward);
        query.Stop();
        Stopwatch checksumWatch = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        checksumWatch.Stop();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(json);
        }

        Assert.Equal(1_000, profiles.Length);
        Assert.Equal(AbilityA, Assert.Single(ward.EducationAttainments).AbilityId);
        output.WriteLine(
            $"education_raw characters={profiles.Length} workflow_ms={workflow.Elapsed.TotalMilliseconds:F3} "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3} "
            + $"checksum_ms={checksumWatch.Elapsed.TotalMilliseconds:F3} "
            + $"json_bytes={json.Length} gzip_bytes={compressed.Length} checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateCampaign(
        CampaignDate? currentDate = null,
        CampaignDate? wardBirthDate = null)
    {
        CampaignDate date = currentDate ?? Date;
        CharacterGuardianshipState guardianship = ActiveGuardianship(date);
        return new CampaignSimulation(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            CharacterSnapshot(wardBirthDate),
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            CharacterResourceWorldSnapshot.Empty,
            CharacterEstateHoldingWorldSnapshot.Empty,
            CharacterMarriageWorldSnapshot.Empty,
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [guardianship]),
            CharacterPregnancyWorldSnapshot.Empty));
    }

    private static CampaignSimulation CreateLargeCampaign(int characterCount)
    {
        EntityId[] fillerIds = Enumerable.Range(0, characterCount - 3)
            .Select(index => new EntityId($"character:test/education_campaign_filler_{index:D4}"))
            .ToArray();
        CharacterDefinition[] definitions = new[]
            {
                Definition(Ward, new CampaignDate(190, 6, 2), []),
                Definition(Teacher, new CampaignDate(160, 1, 1), [AbilityA, AbilityB]),
                Definition(OtherTeacher, new CampaignDate(158, 1, 1), [AbilityA, AbilityB]),
            }
            .Concat(fillerIds.Select(id => Definition(id, new CampaignDate(170, 1, 1), [])))
            .OrderBy(item => item.Id)
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            new[] { Identity(AbilityA), Identity(AbilityB) }.OrderBy(item => item.Id).ToArray(),
            definitions,
            [],
            [],
            definitions.Select(item => new CharacterState(
                    CharacterContractVersions.State,
                    item.Id,
                    [],
                    [],
                    CharacterConditionState.Default,
                    []))
                .ToArray(),
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
            CharacterMarriageWorldSnapshot.Empty,
            new CharacterGuardianshipWorldSnapshot(
                CharacterGuardianshipContractVersions.Snapshot,
                [ActiveGuardianship()]),
            CharacterPregnancyWorldSnapshot.Empty));
    }

    private static CharacterWorldSnapshot CharacterSnapshot(CampaignDate? wardBirthDate)
    {
        CharacterIdentityDefinition[] identities =
        [
            Identity(AbilityA),
            Identity(AbilityB),
        ];
        CharacterDefinition[] definitions =
        [
            Definition(Ward, wardBirthDate ?? new CampaignDate(190, 6, 2), []),
            Definition(Teacher, new CampaignDate(160, 1, 1), [AbilityA, AbilityB]),
            Definition(OtherTeacher, new CampaignDate(158, 1, 1), [AbilityA, AbilityB]),
        ];
        CharacterState[] states = definitions
            .Select(item => new CharacterState(
                CharacterContractVersions.State,
                item.Id,
                [],
                [],
                CharacterConditionState.Default,
                []))
            .OrderBy(item => item.CharacterId)
            .ToArray();
        return new CharacterWorldSnapshot(
            CharacterContractVersions.Snapshot,
            identities.OrderBy(item => item.Id).ToArray(),
            definitions.OrderBy(item => item.Id).ToArray(),
            [],
            [],
            states,
            [],
            []);
    }

    private static CharacterIdentityDefinition Identity(EntityId id) => new(
        CharacterContractVersions.Definition,
        id,
        CharacterIdentityKind.Ability,
        new EntityId($"loc:test/{id.Value.Replace(':', '/')}"));

    private static CharacterDefinition Definition(
        EntityId id,
        CampaignDate birthDate,
        IReadOnlyList<EntityId> abilities)
    {
        EntityId nameKey = new($"loc:test/{id.Value.Replace(':', '/')}");
        return new CharacterDefinition(
            CharacterContractVersions.Definition,
            id,
            nameKey,
            birthDate,
            abilities.Order().ToArray(),
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

    private static CharacterGuardianshipState ActiveGuardianship(CampaignDate? date = null)
    {
        CampaignDate establishedDate = (date ?? Date).AddDays(-30);
        EntityId commandId = new("command:test/education-campaign-guardianship");
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(establishedDate, commandId);
        return new CharacterGuardianshipState(
            CharacterGuardianshipContractVersions.State,
            CharacterGuardianshipIds.DeriveGuardianshipId(eventId, Ward, Teacher),
            Ward,
            Teacher,
            establishedDate,
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

    private static CampaignCommand EducationCommand(
        CampaignSimulation simulation,
        string suffix,
        EntityId abilityId,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands,
        int priority = 0,
        CampaignDate? issuedDate = null) => EducationCommand(
        simulation,
        new EntityId($"command:test/education-campaign-{suffix}"),
        abilityId,
        issuingActor,
        phase,
        priority,
        issuedDate);

    private static CampaignCommand EducationCommand(
        CampaignSimulation simulation,
        EntityId commandId,
        EntityId abilityId,
        EntityId? issuingActor = null,
        ResolutionPhase phase = ResolutionPhase.Commands,
        int priority = 0,
        CampaignDate? issuedDate = null) => CampaignCommand.Create(
        commandId,
        issuingActor ?? CharacterFamilySystem.AuthoritativeActorId,
        issuedDate ?? simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new CompletePrimaryGuardianEducationAction(
                Ward,
                ActiveGuardianship(simulation.World.Calendar.Date).GuardianshipId,
                abilityId)),
        phase,
        priority);

    private static CampaignCommand EndGuardianshipCommand(
        CampaignSimulation simulation,
        string suffix,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/education-campaign-{suffix}"),
        CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new EndPrimaryGuardianshipAction(
                Ward,
                ActiveGuardianship(simulation.World.Calendar.Date).GuardianshipId,
                CharacterGuardianshipEndReason.Revoked)),
        ResolutionPhase.Commands,
        priority);

    private static CampaignCommand ReplaceGuardianshipCommand(
        CampaignSimulation simulation,
        string suffix,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/education-campaign-{suffix}"),
        CharacterFamilySystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterFamilyActionCommandPayload(
            new ReplacePrimaryGuardianshipAction(
                Ward,
                ActiveGuardianship(simulation.World.Calendar.Date).GuardianshipId,
                OtherTeacher)),
        ResolutionPhase.Commands,
        priority);

    private static CampaignCommand IncapacitateWardCommand(
        CampaignSimulation simulation,
        string suffix,
        int priority = 0) => CampaignCommand.Create(
        new EntityId($"command:test/education-campaign-{suffix}"),
        CharacterConditionSystem.AuthoritativeActorId,
        simulation.World.Calendar.Date,
        new CharacterConditionActionCommandPayload(
            new IncapacitateCharacterAction(Ward, CharacterConditionState.Default)),
        ResolutionPhase.Commands,
        priority);

    private static (CampaignEvent Event, PrimaryGuardianEducationCompletedOutcome Outcome)
        PlanEvent(CampaignSimulation simulation, CampaignCommand command)
    {
        EntityId eventId = CharacterFamilyIds.DeriveActionEventId(
            command.IssuedDate,
            command.CommandId);
        CharacterFamilyAggregatePlan plan = simulation.World.PrepareCharacterFamilyAction(
            command.IssuingActor,
            Assert.IsType<CharacterFamilyActionCommandPayload>(command.Payload),
            command.IssuedDate,
            simulation.World.Calendar.TurnIndex,
            command.CommandId,
            eventId);
        CampaignEvent campaignEvent = new(
            ContractVersions.CampaignEvent,
            eventId,
            command.CommandId,
            command.IssuedDate,
            command.Phase,
            command.Priority,
            WorldState.GetCharacterFamilyActionAffectedIds(plan.ResolvedPayload),
            plan.ResolvedPayload);
        return (
            campaignEvent,
            Assert.IsType<PrimaryGuardianEducationCompletedOutcome>(
                plan.ResolvedPayload.Outcome));
    }

    private static (EntityId Earlier, EntityId Later) OrderedCommandIds(
        string firstSuffix,
        string secondSuffix)
    {
        EntityId first = new($"command:test/education-campaign-{firstSuffix}");
        EntityId second = new($"command:test/education-campaign-{secondSuffix}");
        return CharacterFamilyIds.DeriveActionEventId(Date, first).CompareTo(
            CharacterFamilyIds.DeriveActionEventId(Date, second)) < 0
                ? (first, second)
                : (second, first);
    }

    private static void AssertEqualUnrelatedSubsystems(
        WorldSnapshot before,
        WorldSnapshot after)
    {
        Assert.Equal(Serialize(before.Geography), Serialize(after.Geography));
        Assert.Equal(Serialize(before.Relationships), Serialize(after.Relationships));
        Assert.Equal(Serialize(before.Careers), Serialize(after.Careers));
        Assert.Equal(Serialize(before.CharacterResources), Serialize(after.CharacterResources));
        Assert.Equal(
            Serialize(before.CharacterEstateHoldings),
            Serialize(after.CharacterEstateHoldings));
        Assert.Equal(Serialize(before.CharacterMarriages), Serialize(after.CharacterMarriages));
        Assert.Equal(Serialize(before.CharacterPregnancies), Serialize(after.CharacterPregnancies));
        Assert.Equal(Serialize(before.Entities), Serialize(after.Entities));
        Assert.Equal(Serialize(before.RandomStreams), Serialize(after.RandomStreams));
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SimulationJson.CreateOptions());
}
