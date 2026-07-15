using System.Text.Json;

namespace Simulation.Core.Tests;

public sealed class RelationshipTests
{
    private static readonly CampaignDate Date = new(200, 5, 10);
    private static readonly EntityId Subject = new("character:test/a");
    private static readonly EntityId Target = new("character:test/b");
    private static readonly EntityId Witness = new("character:test/c");
    private static readonly EntityId Meaning = new("memory_meaning:test/shared_experience");

    [Fact]
    public void StableIdsUseExactVersionOneSha256Framing()
    {
        Assert.Equal(
            "relationship:sha256/df2f7026173f9c645197c368beebf772aa01d73d4513a19e18cdd8f0b6ce0fbb",
            RelationshipIds.DeriveRelationshipId(Subject, Target).Value);
        Assert.Equal(
            "memory:sha256/a4e074aa37f0c740e8584fbd80d8659ca21ffb94bd5619ef5c20b827ebf7a594",
            RelationshipIds.DeriveMemoryId(Date, new EntityId("command:test/action")).Value);
        Assert.NotEqual(
            RelationshipIds.DeriveRelationshipId(Subject, Target),
            RelationshipIds.DeriveRelationshipId(Target, Subject));
        Assert.NotEqual(
            EventId(new EntityId("command:test/action"), Date),
            EventId(new EntityId("command:test/action"), Date.AddDays(1)));
        Assert.Throws<ArgumentException>(() => RelationshipIds.DeriveRelationshipId(default, Target));
        Assert.Throws<ArgumentException>(() => RelationshipIds.DeriveRelationshipId(Subject, Subject));
        Assert.Throws<ArgumentException>(() => RelationshipIds.DeriveMemoryId(default, new EntityId("command:test/action")));
        Assert.Throws<ArgumentException>(() => RelationshipIds.DeriveMemoryId(Date, default));
    }

    [Fact]
    public void ActionRequiresExistingDifferentCharactersAndNonzeroBoundedImpact()
    {
        RelationshipWorldState state = CreateState(3);

        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Subject, Impact(affection: 1)),
            Date,
            0), "self_relationship");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(new EntityId("character:test/missing"), Impact(affection: 1)),
            Date,
            0), "unknown_target");
        AssertInvalid(state.ValidateAction(
            new EntityId("character:test/missing"),
            Payload(Target, Impact(affection: 1)),
            Date,
            0), "unknown_subject");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact()),
            Date,
            0), "empty_impact");

        ApplyAction(state, Subject, Payload(Target, Impact(affection: 100)), 0);
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(affection: 1)),
            Date,
            1), "relationship_bounds");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(affection: int.MaxValue)),
            Date,
            1), "numeric_overflow");
        Assert.Equal(
            100,
            Assert.Single(AssertHistory(state, Subject).DetailedRelationships).Dimensions.Affection);
    }

    [Fact]
    public void AttractionUsesAgeOnExactResolutionDate()
    {
        CharacterWorldState characters = CreateCharacters(
            [Subject, Target],
            new Dictionary<EntityId, CampaignDate>
            {
                [Subject] = new CampaignDate(200, 5, 11),
                [Target] = new CampaignDate(190, 1, 1),
            },
            new CampaignDate(218, 5, 10));
        RelationshipWorldState state = new(
            RelationshipWorldSnapshot.Empty,
            characters,
            new CampaignCalendar(new CampaignDate(218, 5, 10), 0));
        RelationshipActionCommandPayload action = Payload(Target, Impact(attraction: 1));

        AssertInvalid(
            state.ValidateAction(Subject, action, new CampaignDate(218, 5, 10), 0),
            "underage_attraction");
        Assert.True(state.ValidateAction(
            Subject,
            action,
            new CampaignDate(218, 5, 11),
            0).IsValid);
    }

    [Fact]
    public void WitnessRulesRequireCanonicalExistingNonparticipantsOnlyForWitnessedMemories()
    {
        RelationshipWorldState state = CreateState(4);
        EntityId other = Character(3);

        Assert.True(state.ValidateAction(
            Subject,
            Payload(
                Target,
                Impact(trust: 1),
                MemoryPublicity.Witnessed,
                new[] { Witness, other }.Order().ToArray()),
            Date,
            0).IsValid);
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Witnessed, []),
            Date,
            0), "invalid_witness_count");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Witnessed, [Witness, other]),
            Date,
            0), "noncanonical_witnesses");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Witnessed, [Subject]),
            Date,
            0), "participant_witness");
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Public, [Witness]),
            Date,
            0), "unexpected_witnesses");
    }

    [Fact]
    public void PlanningDoesNotMutateAndApplyingEventMutatesOnlyTheSubjectDirection()
    {
        RelationshipWorldState state = CreateState(3);
        EntityId commandId = new("command:test/one");
        EntityId eventId = EventId(commandId);
        RelationshipActionResolvedEventPayload planned = state.PlanAction(
            Subject,
            Payload(Target, Impact(trust: 4), MemoryPublicity.Witnessed, [Witness]),
            Date,
            0,
            commandId,
            eventId);

        Assert.Empty(state.Subjects);
        state.Apply(CreateEvent(planned, commandId, Date), 0);

        SubjectRelationshipHistory history = AssertHistory(state, Subject);
        DetailedDirectionalRelationship relationship = Assert.Single(history.DetailedRelationships);
        Assert.Equal(4, relationship.Dimensions.Trust);
        ConsequentialMemory memory = Assert.Single(relationship.Memories);
        Assert.Equal(planned.Memory.MemoryId, memory.MemoryId);
        Assert.Equal(eventId, memory.SourceRelationshipActionEventId);
        Assert.False(state.TryGetSubjectHistory(Target, out _));
    }

    [Fact]
    public void ReusedRelationshipCommandOnLaterDateGetsDistinctEventAndMemoryIds()
    {
        WorldState world = WorldState.Create(
            Date,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(3).CaptureSnapshot());
        CampaignSimulation simulation = new(world);
        EntityId commandId = new("command:test/reused-on-later-date");
        CampaignCommand first = CampaignCommand.Create(
            commandId,
            Subject,
            world.Calendar.Date,
            Payload(Target, Impact(trust: 1)));
        Assert.True(simulation.Submit(first).IsValid);
        CampaignEvent firstEvent = Assert.Single(simulation.ResolveTurn());

        CampaignCommand second = CampaignCommand.Create(
            commandId,
            Subject,
            world.Calendar.Date,
            Payload(Target, Impact(trust: 1)));
        Assert.True(simulation.Submit(second).IsValid);
        CampaignEvent secondEvent = Assert.Single(simulation.ResolveTurn());

        Assert.NotEqual(firstEvent.EventId, secondEvent.EventId);
        ConsequentialMemory[] memories = AssertHistory(world.Relationships, Subject)
            .DetailedRelationships
            .Single()
            .Memories
            .OrderBy(memory => memory.RecordedTurnIndex)
            .ToArray();
        Assert.Equal(2, memories.Length);
        Assert.NotEqual(memories[0].MemoryId, memories[1].MemoryId);
        Assert.NotEqual(memories[0].SourceRelationshipActionEventId, memories[1].SourceRelationshipActionEventId);

        EntityId tooLong = new($"command:{new string('a', 152)}");
        CampaignCommand tooLongCommand = CampaignCommand.Create(
            tooLong,
            Subject,
            world.Calendar.Date,
            Payload(Target, Impact(trust: 1)));
        CommandValidationResult tooLongResult = simulation.Submit(tooLongCommand);
        AssertInvalid(tooLongResult, "invalid_event_id");
        WorldSnapshot invalidPending = world.CaptureSnapshot() with
        {
            PendingCommands = [tooLongCommand],
        };
        Assert.Throws<SimulationValidationException>(() => WorldState.Restore(invalidPending));
    }

    [Fact]
    public void ApplyRejectsMismatchedEventCausalityWithoutMutation()
    {
        RelationshipWorldState state = CreateState(3);
        EntityId commandId = new("command:test/causal");
        RelationshipActionResolvedEventPayload planned = state.PlanAction(
            Subject,
            Payload(Target, Impact(respect: 2)),
            Date,
            0,
            commandId,
            EventId(commandId));
        CampaignEvent valid = CreateEvent(planned, commandId, Date);

        CampaignEvent[] invalid =
        [
            valid with { ContractVersion = ContractVersions.CampaignEvent + 1 },
            valid with { CausalId = new EntityId("command:test/wrong") },
            valid with { ResolutionDate = Date.AddDays(1) },
            valid with { AffectedIds = [Subject, Target] },
            valid with
            {
                Payload = planned with
                {
                    RelationshipId = RelationshipIds.DeriveRelationshipId(Target, Subject),
                },
            },
            valid with
            {
                Payload = planned with
                {
                    Memory = planned.Memory with { RecordedTurnIndex = 1 },
                },
            },
        ];
        foreach (CampaignEvent campaignEvent in invalid)
        {
            Assert.Throws<SimulationValidationException>(() => state.Apply(campaignEvent, 0));
            Assert.Empty(state.Subjects);
        }
    }

    [Fact]
    public void EffectiveSeverityDecaysByCompletedIntervalsWithoutTurnMutation()
    {
        ConsequentialMemory permanent = Memory(
            Subject,
            Target,
            new EntityId("command:test/permanent"),
            severity: 7,
            recordedTurn: 3,
            decay: 0);
        ConsequentialMemory decaying = Memory(
            Subject,
            Target,
            new EntityId("command:test/decaying"),
            severity: 3,
            recordedTurn: 3,
            decay: 2);

        Assert.Equal(7, RelationshipWorldState.GetEffectiveSeverity(permanent, 100));
        Assert.Equal(3, RelationshipWorldState.GetEffectiveSeverity(decaying, 4));
        Assert.Equal(2, RelationshipWorldState.GetEffectiveSeverity(decaying, 5));
        Assert.Equal(0, RelationshipWorldState.GetEffectiveSeverity(decaying, 9));
        Assert.Throws<SimulationValidationException>(
            () => RelationshipWorldState.GetEffectiveSeverity(decaying, 2));
    }

    [Fact]
    public void MemoryRetentionIsBoundedAndUsesSeverityTurnThenIdOrdering()
    {
        RelationshipWorldState state = CreateState(3);
        for (int index = 0; index < 18; index++)
        {
            int severity = index is 0 or 1 ? 1 : 50;
            ApplyAction(
                state,
                Subject,
                Payload(Target, Impact(affection: 1), severity: severity),
                index,
                $"memory-{index:D2}");
        }

        DetailedDirectionalRelationship relationship = Assert.Single(
            AssertHistory(state, Subject).DetailedRelationships);
        Assert.Equal(16, relationship.Memories.Count);
        Assert.Equal(2, relationship.FoldedMemories.MemoryCount);
        Assert.Equal(2, relationship.FoldedMemories.TotalEffectiveSeverity);
        Assert.DoesNotContain(
            relationship.Memories,
            item => item.InitialSeverity == 1);
        Assert.Equal(
            relationship.Memories.OrderByDescending(item => item.RecordedTurnIndex).Select(item => item.MemoryId),
            relationship.Memories.Select(item => item.MemoryId));
    }

    [Fact]
    public void MemoryIdBreaksRetentionTiesOrdinally()
    {
        RelationshipWorldState state = CreateState(3);
        for (int index = 0; index < 17; index++)
        {
            ApplyAction(
                state,
                Subject,
                Payload(Target, Impact(affection: 1), severity: 10),
                0,
                $"tie-{index:D2}");
        }

        DetailedDirectionalRelationship relationship = Assert.Single(
            AssertHistory(state, Subject).DetailedRelationships);
        EntityId[] allIds = Enumerable.Range(0, 17)
            .Select(index => RelationshipIds.DeriveMemoryId(
                Date,
                new EntityId($"command:test/tie-{index:D2}")))
            .Order()
            .ToArray();
        Assert.Equal(allIds.Take(16), relationship.Memories.Select(item => item.MemoryId));
        Assert.Equal(1, relationship.FoldedMemories.MemoryCount);
    }

    [Fact]
    public void DetailedAndArchivedHistoriesStayBoundedAndEvictDeterministically()
    {
        RelationshipWorldState state = CreateState(194);
        for (int index = 1; index <= 193; index++)
        {
            ApplyAction(
                state,
                Subject,
                Payload(Character(index), Impact(affection: 1), severity: 1),
                index,
                $"link-{index:D3}");
        }

        SubjectRelationshipHistory history = AssertHistory(state, Subject);
        Assert.Equal(64, history.DetailedRelationships.Count);
        Assert.Equal(128, history.ArchivedRelationships.Count);
        Assert.Equal(1, history.DistantHistory.RelationshipCount);
        Assert.Equal(1, history.DistantHistory.MemoryCount);
        Assert.Equal(2, history.DistantHistory.TotalRecordedImportance);
        Assert.Equal(1, history.DistantHistory.TotalEffectiveMemorySeverity);
        Assert.Equal(Character(193), history.DetailedRelationships[0].TargetCharacterId);
        Assert.Equal(Character(130), history.DetailedRelationships[^1].TargetCharacterId);
        Assert.Equal(Character(129), history.ArchivedRelationships[0].TargetCharacterId);
        Assert.Equal(Character(2), history.ArchivedRelationships[^1].TargetCharacterId);
    }

    [Fact]
    public void ArchivedHistoryReactivatesWithExactDimensions()
    {
        RelationshipWorldState state = CreateState(66);
        for (int index = 1; index <= 65; index++)
        {
            ApplyAction(
                state,
                Subject,
                Payload(Character(index), Impact(affection: 1)),
                index,
                $"archive-{index:D2}");
        }

        Assert.Contains(
            AssertHistory(state, Subject).ArchivedRelationships,
            item => item.TargetCharacterId == Character(1));
        ApplyAction(
            state,
            Subject,
            Payload(Character(1), Impact(trust: 1)),
            66,
            "reactivate");

        DetailedDirectionalRelationship restored = AssertHistory(state, Subject).DetailedRelationships
            .Single(item => item.TargetCharacterId == Character(1));
        Assert.Equal(1, restored.Dimensions.Affection);
        Assert.Equal(1, restored.Dimensions.Trust);
        Assert.Equal(1, restored.FoldedMemories.MemoryCount);
        Assert.DoesNotContain(
            AssertHistory(state, Subject).ArchivedRelationships,
            item => item.TargetCharacterId == Character(1));
    }

    [Fact]
    public void DistantOnlyHistoryRestartsNeutralWithTheSameDirectionalIdentity()
    {
        RelationshipWorldState state = CreateState(194);
        for (int index = 1; index <= 193; index++)
        {
            ApplyAction(
                state,
                Subject,
                Payload(Character(index), Impact(affection: 1)),
                index,
                $"distant-{index:D3}");
        }

        ApplyAction(
            state,
            Subject,
            Payload(Character(1), Impact(trust: 1)),
            194,
            "restart");

        SubjectRelationshipHistory history = AssertHistory(state, Subject);
        DetailedDirectionalRelationship restarted = history.DetailedRelationships
            .Single(item => item.TargetCharacterId == Character(1));
        Assert.Equal(RelationshipIds.DeriveRelationshipId(Subject, Character(1)), restarted.RelationshipId);
        Assert.Equal(0, restarted.Dimensions.Affection);
        Assert.Equal(1, restarted.Dimensions.Trust);
        Assert.Equal(2, history.DistantHistory.RelationshipCount);
    }

    [Fact]
    public void ImpossibleDistantCounterOverflowIsControlledAndAtomic()
    {
        RelationshipWorldState seed = CreateState(194);
        for (int index = 1; index <= 192; index++)
        {
            ApplyAction(
                seed,
                Subject,
                Payload(Character(index), Impact(affection: 1)),
                index,
                $"overflow-seed-{index:D3}");
        }

        RelationshipWorldSnapshot snapshot = seed.CaptureSnapshot();
        SubjectRelationshipHistory source = Assert.Single(snapshot.Subjects);
        RelationshipWorldSnapshot nearOverflow = snapshot with
        {
            Subjects =
            [
                source with
                {
                    DistantHistory = new DistantRelationshipHistoryAggregate(
                        long.MaxValue,
                        long.MaxValue,
                        long.MaxValue,
                        long.MaxValue,
                        Date,
                        Date,
                        192),
                },
            ],
        };
        RelationshipWorldState state = new(
            nearOverflow,
            CreateCharacters(194),
            new CampaignCalendar(Date, 192));
        string before = Serialize(state.CaptureSnapshot());

        Assert.Throws<SimulationValidationException>(() => ApplyAction(
            state,
            Subject,
            Payload(Character(193), Impact(affection: 1)),
            193,
            "overflow"));
        Assert.Equal(before, Serialize(state.CaptureSnapshot()));
    }

    [Fact]
    public void SnapshotConstructionIsInputOrderInvariantAndQueriesAreDefensive()
    {
        RelationshipWorldState state = CreateState(4);
        ApplyAction(
            state,
            Subject,
            Payload(Target, Impact(affection: 1), MemoryPublicity.Witnessed, [Witness]),
            0,
            "first");
        ApplyAction(state, Subject, Payload(Character(3), Impact(trust: 2)), 1, "second");
        RelationshipWorldSnapshot canonical = state.CaptureSnapshot();
        SubjectRelationshipHistory subject = Assert.Single(canonical.Subjects);
        RelationshipWorldSnapshot shuffled = canonical with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships = subject.DetailedRelationships.Reverse().ToArray(),
                },
            ],
        };
        RelationshipWorldState restored = new(
            shuffled,
            CreateCharacters(4),
            new CampaignCalendar(Date, 1));

        Assert.Equal(Serialize(canonical), Serialize(restored.CaptureSnapshot()));
        SubjectRelationshipHistory returned = AssertHistory(restored, Subject);
        ((DetailedDirectionalRelationship[])returned.DetailedRelationships)[0] =
            returned.DetailedRelationships[0] with { RecordedImportance = 999 };
        ConsequentialMemory witnessed = restored.Subjects.Single().DetailedRelationships
            .SelectMany(item => item.Memories)
            .Single(item => item.Publicity == MemoryPublicity.Witnessed);
        ((EntityId[])witnessed.WitnessIds)[0] = Character(3);

        Assert.Equal(Serialize(canonical), Serialize(restored.CaptureSnapshot()));
    }

    [Fact]
    public void SnapshotRejectsUnsupportedDuplicateNoncanonicalAndCollisionState()
    {
        RelationshipWorldState state = CreateState(4);
        ApplyAction(
            state,
            Subject,
            Payload(Target, Impact(affection: 1), MemoryPublicity.Witnessed, [Witness]),
            0,
            "valid");
        RelationshipWorldSnapshot valid = state.CaptureSnapshot();
        SubjectRelationshipHistory subject = Assert.Single(valid.Subjects);
        DetailedDirectionalRelationship relationship = Assert.Single(subject.DetailedRelationships);
        ConsequentialMemory memory = Assert.Single(relationship.Memories);
        CharacterWorldState characters = CreateCharacters(4);
        CampaignCalendar calendar = new(Date, 0);

        AssertInvalidSnapshot(valid with { ContractVersion = 2 }, characters, calendar);
        AssertInvalidSnapshot(valid with { Subjects = [subject, subject with { }] }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with { RelationshipId = new EntityId("relationship:test/wrong") },
                    ],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            RecordedImportance = relationship.RecordedImportance + 1,
                        },
                    ],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            Memories =
                            [
                                memory with { WitnessIds = [Witness, Witness] },
                            ],
                        },
                    ],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            Memories = [memory, memory with { }],
                        },
                    ],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            Memories =
                            [
                                memory with
                                {
                                    AppliedImpact = Impact(affection: int.MaxValue),
                                },
                            ],
                        },
                    ],
                },
            ],
        }, characters, calendar);

        EntityId causalCommand = new("command:test/valid");
        CampaignDate beforeBirth = Date.AddDays(-1);
        ConsequentialMemory earlierMemory = memory with
        {
            ResolutionDate = beforeBirth,
            MemoryId = RelationshipIds.DeriveMemoryId(beforeBirth, causalCommand),
            SourceRelationshipActionEventId = EventId(causalCommand, beforeBirth),
        };
        CharacterWorldState bornOnCurrentDate = CreateCharacters(
            [Subject, Target, Witness, Character(3)],
            new Dictionary<EntityId, CampaignDate> { [Subject] = Date },
            Date);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            LastChangeDate = beforeBirth,
                            Memories = [earlierMemory],
                        },
                    ],
                },
            ],
        }, bornOnCurrentDate, calendar);
        CharacterWorldState witnessBornOnCurrentDate = CreateCharacters(
            [Subject, Target, Witness, Character(3)],
            new Dictionary<EntityId, CampaignDate> { [Witness] = Date },
            Date);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            LastChangeDate = beforeBirth,
                            Memories = [earlierMemory],
                        },
                    ],
                },
            ],
        }, witnessBornOnCurrentDate, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            FoldedMemories = new FoldedMemorySummary(
                                1,
                                1,
                                beforeBirth,
                                beforeBirth),
                        },
                    ],
                },
            ],
        }, bornOnCurrentDate, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            LastChangeDate = beforeBirth,
                            Memories = [earlierMemory],
                            FoldedMemories = new FoldedMemorySummary(1, 1, Date, Date),
                        },
                    ],
                },
            ],
        }, characters, calendar);

        ArchivedDirectionalRelationshipSummary malformedArchive = new(
            RelationshipContractVersions.State,
            relationship.RelationshipId,
            relationship.SubjectCharacterId,
            relationship.TargetCharacterId,
            relationship.Dimensions,
            relationship.RecordedImportance,
            relationship.LastChangeDate,
            relationship.LastChangeTurnIndex,
            new FoldedMemorySummary(
                1,
                memory.InitialSeverity,
                beforeBirth,
                beforeBirth));
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships = [],
                    ArchivedRelationships = [malformedArchive],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships = [],
                    ArchivedRelationships = [],
                    DistantHistory = new DistantRelationshipHistoryAggregate(
                        1,
                        1,
                        1,
                        1,
                        beforeBirth,
                        beforeBirth,
                        0),
                },
            ],
        }, bornOnCurrentDate, calendar);

        CharacterWorldState underageSubject = CreateCharacters(
            [Subject, Target, Witness, Character(3)],
            new Dictionary<EntityId, CampaignDate>
            {
                [Subject] = new CampaignDate(183, 5, 11),
            },
            Date);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            Dimensions = relationship.Dimensions with { Attraction = 1 },
                            RecordedImportance = relationship.RecordedImportance + 1,
                        },
                    ],
                },
            ],
        }, underageSubject, calendar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void EveryDimensionChangesIndependentlyAndNeverClamps(int dimension)
    {
        RelationshipWorldState state = CreateState(3);
        RelationshipImpact impact = dimension switch
        {
            0 => Impact(affection: 100),
            1 => Impact(trust: 100),
            2 => Impact(respect: 100),
            3 => Impact(attraction: 100),
            4 => Impact(obligation: 100),
            5 => Impact(fear: 100),
            6 => Impact(resentment: 100),
            7 => Impact(rivalry: 100),
            8 => Impact(compatibility: -100),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };

        ApplyAction(state, Subject, Payload(Target, impact), 0, $"dimension-{dimension}");

        RelationshipDimensions dimensions = Assert.Single(
            AssertHistory(state, Subject).DetailedRelationships).Dimensions;
        int[] actual =
        [
            dimensions.Affection,
            dimensions.Trust,
            dimensions.Respect,
            dimensions.Attraction,
            dimensions.Obligation,
            dimensions.Fear,
            dimensions.Resentment,
            dimensions.Rivalry,
            dimensions.Compatibility,
        ];
        Assert.Equal(dimension == 8 ? -100 : 100, actual[dimension]);
        Assert.All(actual.Where((_, index) => index != dimension), value => Assert.Equal(0, value));

        RelationshipImpact beyond = dimension switch
        {
            0 => Impact(affection: 1),
            1 => Impact(trust: 1),
            2 => Impact(respect: 1),
            3 => Impact(attraction: 1),
            4 => Impact(obligation: 1),
            5 => Impact(fear: 1),
            6 => Impact(resentment: 1),
            7 => Impact(rivalry: 1),
            8 => Impact(compatibility: -1),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };
        AssertInvalid(state.ValidateAction(Subject, Payload(Target, beyond), Date, 1), "relationship_bounds");
        Assert.Equal(dimension == 8 ? -100 : 100, actual[dimension]);
    }

    [Fact]
    public void RelationshipCharacterActorsDoNotGainSyntheticOrGeographicAuthority()
    {
        ArmyGeographicState army = GeographyFixture.Army(
            "relationship-authority",
            GeographyFixture.FactionOne,
            GeographyFixture.A);
        CharacterWorldSnapshot characters = CreateCharacters(3).CaptureSnapshot();
        SyntheticEntitySnapshot syntheticActor = new(
            GeographyFixture.Actor,
            SimulationTier.Full,
            1,
            1,
            1,
            []);
        WorldState world = WorldState.Create(
            Date,
            1,
            [syntheticActor],
            GeographyFixture.Snapshot([army]),
            characters);
        CampaignSimulation simulation = new(world);

        Assert.True(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/relationship-authority"),
            Subject,
            Date,
            Payload(Target, Impact(trust: 1)))).IsValid);
        AssertInvalid(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/character-resources"),
            Subject,
            Date,
            new AdjustResourcesCommandPayload(GeographyFixture.Actor, 0, 1, 0))), "unknown_actor");
        AssertInvalid(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/synthetic-relationship"),
            GeographyFixture.Actor,
            Date,
            Payload(Target, Impact(trust: 1)))), "unknown_actor");

        MovementOrderPayload movement = new(
            army.ArmyId,
            [GeographyFixture.RoadAb],
            TransportMode.Foot,
            MovementStance.Normal,
            Date,
            MovementFallback.Wait);
        AssertInvalid(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/character-movement"),
            Subject,
            Date,
            movement)), "unknown_actor");
        Assert.True(simulation.Submit(CampaignCommand.Create(
            new EntityId("command:test/synthetic-movement"),
            GeographyFixture.Actor,
            Date,
            movement)).IsValid);
    }

    [Fact]
    public void EarlierRelationshipOutcomeInvalidatesLaterCommandWithControlledCancellation()
    {
        WorldState world = WorldState.Create(
            Date,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(3).CaptureSnapshot());
        CampaignSimulation simulation = new(world);
        foreach (string suffix in new[] { "a", "b" })
        {
            Assert.True(simulation.Submit(CampaignCommand.Create(
                new EntityId($"command:test/revalidate-{suffix}"),
                Subject,
                Date,
                Payload(Target, Impact(affection: 60)))).IsValid);
        }

        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();

        Assert.IsType<RelationshipActionResolvedEventPayload>(events[0].Payload);
        CommandCancelledEventPayload cancellation = Assert.IsType<CommandCancelledEventPayload>(events[1].Payload);
        Assert.Equal("command_invalidated", cancellation.ReasonCode);
        DetailedDirectionalRelationship relationship = Assert.Single(world.Relationships.Subjects)
            .DetailedRelationships.Single();
        Assert.Equal(60, relationship.Dimensions.Affection);
        Assert.Single(relationship.Memories);
    }

    [Theory]
    [InlineData("actor", "actor_unavailable")]
    [InlineData("target", "command_invalidated")]
    public void RemovedRelationshipParticipantCancelsPendingCommandAtResolution(
        string removed,
        string expectedReason)
    {
        WorldSnapshot source = WorldState.Create(
            Date,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(3).CaptureSnapshot()).CaptureSnapshot();
        CampaignCommand pending = CampaignCommand.Create(
            new EntityId($"command:test/removed-{removed}"),
            Subject,
            Date,
            Payload(Target, Impact(trust: 1)));
        EntityId[] retainedCharacters = removed == "actor"
            ? [Target, Witness]
            : [Subject, Witness];
        WorldSnapshot invalidated = source with
        {
            Characters = CreateCharacters(
                retainedCharacters,
                new Dictionary<EntityId, CampaignDate>(),
                Date).CaptureSnapshot(),
            PendingCommands = [pending],
        };
        CampaignSimulation simulation = new(WorldState.Restore(invalidated));

        CampaignEvent campaignEvent = Assert.Single(simulation.ResolveTurn());

        CommandCancelledEventPayload cancellation = Assert.IsType<CommandCancelledEventPayload>(
            campaignEvent.Payload);
        Assert.Equal(expectedReason, cancellation.ReasonCode);
        Assert.Empty(simulation.World.Relationships.Subjects);
    }

    [Fact]
    public void PendingDiagnosticsRestoreAndSaveRoundTripRegisteredRelationshipPayloads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"relationship-save-{Guid.NewGuid():N}.save.gz");
        try
        {
            WorldState world = WorldState.Create(
                Date,
                1,
                [],
                GeographicWorldSnapshot.Empty,
                CreateCharacters(3).CaptureSnapshot());
            CampaignSimulation simulation = new(world);
            CampaignDate dueDate = Date.AddDays(world.Calendar.DaysInCurrentTurn);
            CampaignCommand pending = CampaignCommand.Create(
                new EntityId("command:test/pending-relationship"),
                Subject,
                dueDate,
                Payload(Target, Impact(respect: 3), MemoryPublicity.Witnessed, [Witness]));
            Assert.True(simulation.Submit(pending).IsValid);

            string snapshotJson = JsonSerializer.Serialize(world.CaptureSnapshot(), SimulationJson.CreateOptions());
            WorldSnapshot restoredPending = JsonSerializer.Deserialize<WorldSnapshot>(
                snapshotJson,
                SimulationJson.CreateOptions())!;
            Assert.IsType<RelationshipActionCommandPayload>(Assert.Single(restoredPending.PendingCommands).Payload);
            Assert.Single(WorldState.Restore(restoredPending).CaptureSnapshot().PendingCommands);
            Assert.Empty(simulation.ResolveTurn());
            CampaignEvent resolved = Assert.Single(simulation.ResolveTurn());
            RelationshipActionResolvedEventPayload payload = Assert.IsType<RelationshipActionResolvedEventPayload>(
                resolved.Payload);
            ConsequentialMemory memory = payload.Memory;
            Assert.Equal(Subject, memory.SubjectCharacterId);
            Assert.Equal(Target, memory.TargetCharacterId);
            Assert.Equal([Witness], memory.WitnessIds);
            Assert.Equal(dueDate, memory.ResolutionDate);
            Assert.Equal(1, memory.RecordedTurnIndex);
            Assert.Equal(Meaning, memory.MeaningId);
            Assert.Equal(10, memory.InitialSeverity);
            Assert.Equal(MemoryPublicity.Witnessed, memory.Publicity);
            Assert.Equal(0, memory.DecayIntervalTurns);
            Assert.Equal(3, memory.AppliedImpact.Respect);
            Assert.Equal(resolved.EventId, memory.SourceRelationshipActionEventId);

            SaveEnvelope envelope = SaveEnvelope.Create(
                "0.1.0",
                [],
                simulation,
                DateTimeOffset.UnixEpoch);
            SaveStore store = new();
            store.SaveAtomic(path, envelope);
            SaveEnvelope loaded = store.Load(path);
            Assert.IsType<RelationshipActionCommandPayload>(Assert.Single(loaded.DiagnosticCommands).Payload);
            Assert.IsType<RelationshipActionResolvedEventPayload>(Assert.Single(loaded.DiagnosticEvents).Payload);
            WorldState restoredWorld = WorldState.Restore(loaded.Snapshot);
            Assert.Single(restoredWorld.Relationships.Subjects.Single().DetailedRelationships.Single().Memories);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnregisteredRelationshipCommandAndEventDiscriminatorsFailDeserialization()
    {
        JsonSerializerOptions options = SimulationJson.CreateOptions();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:test/discriminator"),
            Subject,
            Date,
            Payload(Target, Impact(trust: 1)));
        string commandJson = JsonSerializer.Serialize(command, options)
            .Replace("relationship_action.v1", "relationship_action.v999", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CampaignCommand>(commandJson, options));

        RelationshipWorldState state = CreateState(3);
        RelationshipActionResolvedEventPayload payload = state.PlanAction(
            Subject,
            (RelationshipActionCommandPayload)command.Payload,
            Date,
            0,
            command.CommandId,
            EventId(command.CommandId));
        string eventJson = JsonSerializer.Serialize(CreateEvent(payload, command.CommandId, Date), options)
            .Replace("relationship_action_resolved.v1", "relationship_action_resolved.v999", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CampaignEvent>(eventJson, options));
    }

    [Fact]
    public void RelationshipChecksumIsOrderInvariantAndMutationSensitive()
    {
        RelationshipWorldState state = CreateState(4);
        ApplyAction(state, Subject, Payload(Target, Impact(affection: 1)), 0, "checksum-a");
        ApplyAction(state, Subject, Payload(Character(3), Impact(trust: 1)), 0, "checksum-b");
        RelationshipWorldSnapshot relationships = state.CaptureSnapshot();
        WorldSnapshot world = WorldState.Create(
            Date,
            1,
            [],
            GeographicWorldSnapshot.Empty,
            CreateCharacters(4).CaptureSnapshot(),
            relationships).CaptureSnapshot();
        SimulationChecksum expected = SimulationChecksum.Compute(world);
        SubjectRelationshipHistory subject = Assert.Single(relationships.Subjects);
        RelationshipWorldSnapshot shuffled = relationships with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships = subject.DetailedRelationships.Reverse().ToArray(),
                },
            ],
        };
        Assert.Equal(expected, SimulationChecksum.Compute(world with { Relationships = shuffled }));

        DetailedDirectionalRelationship first = subject.DetailedRelationships[0];
        RelationshipWorldSnapshot mutated = relationships with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        first with
                        {
                            Dimensions = first.Dimensions with
                            {
                                Affection = first.Dimensions.Affection + 1,
                            },
                            RecordedImportance = first.RecordedImportance + 1,
                        },
                        .. subject.DetailedRelationships.Skip(1),
                    ],
                },
            ],
        };
        Assert.NotEqual(expected, SimulationChecksum.Compute(world with { Relationships = mutated }));
    }

    [Fact]
    public void SnapshotRejectsNullCollectionsEntriesUnsupportedMemoryAndMistypedCharacters()
    {
        RelationshipWorldState state = CreateState(3);
        ApplyAction(state, Subject, Payload(Target, Impact(trust: 1)), 0, "malformed");
        RelationshipWorldSnapshot valid = state.CaptureSnapshot();
        SubjectRelationshipHistory subject = Assert.Single(valid.Subjects);
        DetailedDirectionalRelationship relationship = Assert.Single(subject.DetailedRelationships);
        ConsequentialMemory memory = Assert.Single(relationship.Memories);
        CharacterWorldState characters = CreateCharacters(3);
        CampaignCalendar calendar = new(Date, 0);

        AssertInvalidSnapshot(valid with { Subjects = null! }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects = [subject with { DetailedRelationships = null! }],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with { Memories = [null!] },
                    ],
                },
            ],
        }, characters, calendar);
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            Memories = [memory with { ContractVersion = 2 }],
                        },
                    ],
                },
            ],
        }, characters, calendar);
        EntityId mistyped = new("synthetic:test/not_a_character");
        AssertInvalidSnapshot(valid with
        {
            Subjects =
            [
                subject with
                {
                    DetailedRelationships =
                    [
                        relationship with
                        {
                            RelationshipId = RelationshipIds.DeriveRelationshipId(Subject, mistyped),
                            TargetCharacterId = mistyped,
                        },
                    ],
                },
            ],
        }, characters, calendar);
    }

    [Fact]
    public void WitnessedMemoryAcceptsExactlyThirtyTwoCanonicalWitnesses()
    {
        RelationshipWorldState state = CreateState(36);
        EntityId[] thirtyTwo = Enumerable.Range(2, 32).Select(Character).Order().ToArray();
        EntityId[] thirtyThree = Enumerable.Range(2, 33).Select(Character).Order().ToArray();

        Assert.True(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Witnessed, thirtyTwo),
            Date,
            0).IsValid);
        AssertInvalid(state.ValidateAction(
            Subject,
            Payload(Target, Impact(trust: 1), MemoryPublicity.Witnessed, thirtyThree),
            Date,
            0), "invalid_witness_count");
    }

    private static RelationshipWorldState CreateState(int characterCount)
    {
        CharacterWorldState characters = CreateCharacters(characterCount);
        return new(
            RelationshipWorldSnapshot.Empty,
            characters,
            new CampaignCalendar(Date, 0));
    }

    private static CharacterWorldState CreateCharacters(
        int count,
        CampaignDate? currentDate = null) => CreateCharacters(
            Enumerable.Range(0, count).Select(Character).ToArray(),
            new Dictionary<EntityId, CampaignDate>(),
            currentDate ?? Date);

    private static CharacterWorldState CreateCharacters(
        IReadOnlyList<EntityId> ids,
        IReadOnlyDictionary<EntityId, CampaignDate> births,
        CampaignDate currentDate)
    {
        CharacterDefinition[] definitions = ids.Select(id =>
        {
            EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}/name");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                nameKey,
                births.TryGetValue(id, out CampaignDate birth) ? birth : new CampaignDate(150, 1, 1),
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
        }).ToArray();
        CharacterState[] states = ids.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            CharacterConditionState.Default)).ToArray();
        return new CharacterWorldState(
            new CharacterWorldSnapshot(
                CharacterContractVersions.Snapshot,
                [],
                definitions,
                [],
                [],
                states,
                [],
                []),
            currentDate);
    }

    private static void ApplyAction(
        RelationshipWorldState state,
        EntityId subject,
        RelationshipActionCommandPayload payload,
        long turn,
        string suffix = "action")
    {
        EntityId commandId = new($"command:test/{suffix}");
        RelationshipActionResolvedEventPayload planned = state.PlanAction(
            subject,
            payload,
            Date,
            turn,
            commandId,
            EventId(commandId));
        state.Apply(CreateEvent(planned, commandId, Date), turn);
    }

    private static CampaignEvent CreateEvent(
        RelationshipActionResolvedEventPayload payload,
        EntityId commandId,
        CampaignDate date)
    {
        EntityId[] affected =
        [
            payload.SubjectCharacterId,
            payload.TargetCharacterId,
            payload.RelationshipId,
            payload.Memory.MemoryId,
        ];
        return new CampaignEvent(
            ContractVersions.CampaignEvent,
            EventId(commandId, date),
            commandId,
            date,
            ResolutionPhase.Commands,
            0,
            affected.Order().ToArray(),
            payload);
    }

    private static RelationshipActionCommandPayload Payload(
        EntityId target,
        RelationshipImpact impact,
        MemoryPublicity publicity = MemoryPublicity.Private,
        IReadOnlyList<EntityId>? witnesses = null,
        int severity = 10,
        int decay = 0) => new(
        target,
        impact,
        Meaning,
        severity,
        publicity,
        decay,
        witnesses ?? []);

    private static RelationshipImpact Impact(
        int affection = 0,
        int trust = 0,
        int respect = 0,
        int attraction = 0,
        int obligation = 0,
        int fear = 0,
        int resentment = 0,
        int rivalry = 0,
        int compatibility = 0) => new(
        affection,
        trust,
        respect,
        attraction,
        obligation,
        fear,
        resentment,
        rivalry,
        compatibility);

    private static ConsequentialMemory Memory(
        EntityId subject,
        EntityId target,
        EntityId commandId,
        int severity,
        long recordedTurn,
        int decay)
    {
        EntityId eventId = EventId(commandId);
        return new ConsequentialMemory(
            RelationshipContractVersions.State,
            RelationshipIds.DeriveMemoryId(Date, commandId),
            subject,
            target,
            [],
            Date,
            recordedTurn,
            Meaning,
            severity,
            MemoryPublicity.Private,
            decay,
            Impact(affection: 1),
            eventId);
    }

    private static EntityId Character(int index) => index switch
    {
        0 => Subject,
        1 => Target,
        2 => Witness,
        _ => new EntityId($"character:test/{index:D3}"),
    };

    private static EntityId EventId(EntityId commandId, CampaignDate? date = null) =>
        RelationshipWorldState.DeriveEventId(date ?? Date, commandId);

    private static SubjectRelationshipHistory AssertHistory(
        RelationshipWorldState state,
        EntityId subject)
    {
        Assert.True(state.TryGetSubjectHistory(subject, out SubjectRelationshipHistory? history));
        return history;
    }

    private static void AssertInvalid(CommandValidationResult result, string issueCode)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    private static void AssertInvalidSnapshot(
        RelationshipWorldSnapshot snapshot,
        IAuthoritativeCharacterWorldQuery characters,
        CampaignCalendar calendar) => Assert.Throws<SimulationValidationException>(
        () => new RelationshipWorldState(snapshot, characters, calendar));

    private static string Serialize(RelationshipWorldSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SimulationJson.CreateOptions());
}
