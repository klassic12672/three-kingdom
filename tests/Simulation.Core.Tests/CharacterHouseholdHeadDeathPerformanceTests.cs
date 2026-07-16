using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterHouseholdHeadDeathPerformanceTests
{
    private readonly ITestOutputHelper output;

    public CharacterHouseholdHeadDeathPerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void F314_ThousandCharacterTwoHundredHeadDeathFixtureRecordsRawLocalPerformance()
    {
        const int characterCount = 1_000;
        const int deathCount = 200;
        CampaignDate date = new(200, 5, 10);
        EntityId[] ids = Enumerable.Range(0, characterCount)
            .Select(index => new EntityId($"character:perf/f3-{index:D4}"))
            .ToArray();
        CharacterDefinition[] definitions = ids.Select(id =>
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
        }).ToArray();
        CharacterState[] states = ids.Select(id => new CharacterState(
            CharacterContractVersions.State,
            id,
            [],
            [],
            CharacterConditionState.Default,
            [])).ToArray();
        HouseholdDefinition[] householdDefinitions = Enumerable.Range(0, deathCount)
            .Select(index => new HouseholdDefinition(
                CharacterContractVersions.Definition,
                new EntityId($"household:perf/f3-{index:D4}"),
                new EntityId($"loc:household/perf_f3_{index:D4}")))
            .ToArray();
        HouseholdState[] householdStates = householdDefinitions.Select((definition, index) =>
            new HouseholdState(
                CharacterContractVersions.State,
                definition.Id,
                ids[index],
                new[] { ids[index], ids[deathCount + index] }.Order().ToArray()))
            .ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            householdDefinitions,
            states,
            [],
            householdStates);
        CampaignSimulation simulation = new(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters));
        for (int index = 0; index < deathCount; index++)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:perf/f3-head-death-{index:D4}"),
                CharacterConditionSystem.AuthoritativeActorId,
                date,
                new CharacterConditionActionCommandPayload(new ResolveHouseholdHeadDeathAction(
                    ids[index],
                    simulation.World.Characters.Profiles[index].Condition,
                    householdDefinitions[index].Id,
                    ids[deathCount + index])));
            Assert.True(simulation.Submit(command).IsValid);
        }

        Stopwatch workflow = Stopwatch.StartNew();
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch query = Stopwatch.StartNew();
        int deadHeads = simulation.World.Characters.Profiles.Count(item =>
            item.Condition.VitalStatus == CharacterVitalStatus.Dead);
        int replacedHeads = simulation.World.Characters.Households.Count(item =>
            item.HeadCharacterId.Value.StartsWith("character:perf/f3-02", StringComparison.Ordinal)
            || item.HeadCharacterId.Value.StartsWith("character:perf/f3-03", StringComparison.Ordinal));
        query.Stop();
        Stopwatch snapshotChecksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, SimulationJson.CreateOptions());
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(json);
        }
        snapshotChecksum.Stop();

        Assert.Equal(deathCount, events.Count);
        Assert.All(events, item => Assert.IsType<HouseholdHeadDeathResolvedOutcome>(
            Assert.IsType<CharacterConditionActionResolvedEventPayload>(item.Payload).Outcome));
        Assert.Equal(deathCount, deadHeads);
        Assert.Equal(deathCount, replacedHeads);
        Assert.Equal(characterCount, snapshot.Characters.CharacterStates.Count);
        Assert.Equal(deathCount, snapshot.Characters.HouseholdStates.Count);
        output.WriteLine(
            $"f3_household_head_death_raw characters={characterCount}; deaths={deathCount}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={snapshotChecksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksum.Value}");
    }
}
