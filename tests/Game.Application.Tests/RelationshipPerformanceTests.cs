using System.Diagnostics;
using Simulation.Core;
using Xunit.Abstractions;

namespace Game.Application.Tests;

public sealed class RelationshipPerformanceTests(ITestOutputHelper output)
{
    private const int CharacterCount = 1_000;
    private const int DetailedLinksPerCharacter = 16;
    private const int MemoriesPerLink = 4;
    private static readonly CampaignDate Date = new(191, 7, 15);
    private static readonly EntityId MeaningId = new("memory_meaning:performance/relationship_action");

    [Fact]
    public void ThousandCharacterRelationshipFixtureRecordsRawLocalPerformance()
    {
        CampaignSimulation simulation = CreateFixture();
        Assert.Equal(CharacterCount, simulation.World.Characters.Profiles.Count);
        Assert.Equal(CharacterCount, simulation.World.Relationships.Subjects.Count);
        Assert.Equal(
            CharacterCount * DetailedLinksPerCharacter,
            simulation.World.Relationships.Subjects.Sum(subject => subject.DetailedRelationships.Count));
        Assert.Equal(
            CharacterCount * DetailedLinksPerCharacter * MemoriesPerLink,
            simulation.World.Relationships.Subjects
                .SelectMany(subject => subject.DetailedRelationships)
                .Sum(relationship => relationship.Memories.Count));

        for (int index = 0; index < CharacterCount; index++)
        {
            EntityId subject = Character(index);
            EntityId target = Character((index + 1) % CharacterCount);
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:performance/turn/{index:D4}"),
                subject,
                Date,
                new RelationshipActionCommandPayload(
                    target,
                    new RelationshipImpact(0, 1, 0, 0, 0, 0, 0, 0, 0),
                    MeaningId,
                    25,
                    MemoryPublicity.Private,
                    0,
                    []));
            CommandValidationResult validation = simulation.Submit(command);
            Assert.True(
                validation.IsValid,
                string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        stopwatch.Stop();
        TimeSpan turnElapsed = stopwatch.Elapsed;
        Assert.Equal(CharacterCount, events.Count);
        Assert.All(events, campaignEvent => Assert.IsType<RelationshipActionResolvedEventPayload>(campaignEvent.Payload));

        RelationshipSummaryQuery query = new(simulation.World);
        stopwatch.Restart();
        bool found = query.TryGet(Character(0), Character(0), out RelationshipSummary? summary);
        stopwatch.Stop();
        TimeSpan queryElapsed = stopwatch.Elapsed;
        Assert.True(found);
        Assert.NotNull(summary);
        Assert.Equal(DetailedLinksPerCharacter, summary.DetailedRelationships.Count);

        stopwatch.Restart();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        stopwatch.Stop();
        TimeSpan snapshotChecksumElapsed = stopwatch.Elapsed;
        Assert.Equal(
            (CharacterCount * DetailedLinksPerCharacter * MemoriesPerLink) + CharacterCount,
            snapshot.Relationships.Subjects
                .SelectMany(subject => subject.DetailedRelationships)
                .Sum(relationship => relationship.Memories.Count));

        SaveEnvelope envelope = SaveEnvelope.Create(
            "0.1.0",
            [],
            simulation,
            DateTimeOffset.UnixEpoch);
        string path = Path.Combine(
            Path.GetTempPath(),
            $"relationship-performance-{Guid.NewGuid():N}.save.gz");
        SaveStore store = new();
        TimeSpan saveElapsed;
        TimeSpan loadElapsed;
        try
        {
            stopwatch.Restart();
            store.SaveAtomic(path, envelope);
            stopwatch.Stop();
            saveElapsed = stopwatch.Elapsed;

            stopwatch.Restart();
            SaveEnvelope loaded = store.Load(path);
            stopwatch.Stop();
            loadElapsed = stopwatch.Elapsed;
            Assert.Equal(checksum.Value, loaded.Checksum);
        }
        finally
        {
            File.Delete(path);
        }

        output.WriteLine($"fixture.characters={CharacterCount}");
        output.WriteLine($"fixture.detailed_relationships={CharacterCount * DetailedLinksPerCharacter}");
        output.WriteLine($"fixture.initial_memories={CharacterCount * DetailedLinksPerCharacter * MemoriesPerLink}");
        output.WriteLine($"fixture.resolved_actions={CharacterCount}");
        output.WriteLine($"turn_processing_ms={turnElapsed.TotalMilliseconds:F3}");
        output.WriteLine($"one_subject_summary_ms={queryElapsed.TotalMilliseconds:F3}");
        output.WriteLine($"snapshot_checksum_ms={snapshotChecksumElapsed.TotalMilliseconds:F3}");
        output.WriteLine($"save_ms={saveElapsed.TotalMilliseconds:F3}");
        output.WriteLine($"load_ms={loadElapsed.TotalMilliseconds:F3}");
        output.WriteLine($"checksum={checksum.Value}");
    }

    private static CampaignSimulation CreateFixture()
    {
        EntityId[] characterIds = Enumerable.Range(0, CharacterCount).Select(Character).ToArray();
        CharacterDefinition[] definitions = characterIds.Select(id => new CharacterDefinition(
            CharacterContractVersions.Definition,
            id,
            new EntityId($"loc:{id.Value.Replace(':', '/')}/name"),
            new CampaignDate(150, 1, 1),
            [],
            [],
            [],
            [],
            [])).ToArray();
        CharacterState[] states = characterIds.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [])).ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);

        SubjectRelationshipHistory[] subjects = Enumerable.Range(0, CharacterCount)
            .Select(subjectIndex => CreateSubjectHistory(subjectIndex))
            .ToArray();
        RelationshipWorldSnapshot relationships = new(
            RelationshipContractVersions.Snapshot,
            subjects);
        WorldState world = WorldState.Create(
            Date,
            20260715,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            relationships);
        return new CampaignSimulation(world);
    }

    private static SubjectRelationshipHistory CreateSubjectHistory(int subjectIndex)
    {
        EntityId subject = Character(subjectIndex);
        DetailedDirectionalRelationship[] detailed = Enumerable.Range(0, DetailedLinksPerCharacter)
            .Select(linkIndex => CreateRelationship(subjectIndex, linkIndex))
            .ToArray();
        return new SubjectRelationshipHistory(
            RelationshipContractVersions.State,
            subject,
            detailed,
            [],
            DistantRelationshipHistoryAggregate.Empty);
    }

    private static DetailedDirectionalRelationship CreateRelationship(int subjectIndex, int linkIndex)
    {
        EntityId subject = Character(subjectIndex);
        EntityId target = Character((subjectIndex + linkIndex + 1) % CharacterCount);
        ConsequentialMemory[] memories = Enumerable.Range(0, MemoriesPerLink)
            .Select(memoryIndex => CreateMemory(subjectIndex, linkIndex, memoryIndex, subject, target))
            .ToArray();
        int memorySeverity = memories.Sum(memory => memory.InitialSeverity);
        RelationshipDimensions dimensions = new(4, 0, 0, 0, 0, 0, 0, 0, 0);
        return new DetailedDirectionalRelationship(
            RelationshipContractVersions.State,
            RelationshipIds.DeriveRelationshipId(subject, target),
            subject,
            target,
            dimensions,
            dimensions.Affection + memorySeverity,
            Date,
            0,
            memories,
            FoldedMemorySummary.Empty);
    }

    private static ConsequentialMemory CreateMemory(
        int subjectIndex,
        int linkIndex,
        int memoryIndex,
        EntityId subject,
        EntityId target)
    {
        EntityId commandId = new(
            $"command:performance/fixture/{subjectIndex:D4}/{linkIndex:D2}/{memoryIndex:D2}");
        EntityId eventId = new(
            $"event:relationship_action/0191-07-15/{commandId.Value.Replace(':', '/')}");
        return new ConsequentialMemory(
            RelationshipContractVersions.State,
            RelationshipIds.DeriveMemoryId(Date, commandId),
            subject,
            target,
            [],
            Date,
            0,
            MeaningId,
            20 + memoryIndex,
            MemoryPublicity.Private,
            0,
            new RelationshipImpact(1, 0, 0, 0, 0, 0, 0, 0, 0),
            eventId);
    }

    private static EntityId Character(int index) => new($"character:performance/{index:D4}");
}
