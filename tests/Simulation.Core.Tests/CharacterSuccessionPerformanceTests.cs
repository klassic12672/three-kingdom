using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionPerformanceTests
{
    private readonly ITestOutputHelper output;

    public CharacterSuccessionPerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void F414_ThousandCharacterTwoHundredDesignationFixtureRecordsRawLocalPerformance()
    {
        const int characterCount = 1_000;
        const int designationCount = 200;
        CampaignDate date = new(200, 5, 10);
        EntityId[] ids = Enumerable.Range(0, characterCount)
            .Select(index => new EntityId($"character:perf/f4-{index:D4}"))
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
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            ids.Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                [],
                [],
                CharacterConditionState.Default,
                [])).ToArray(),
            [],
            []);
        CampaignSimulation simulation = new(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters));
        for (int index = 0; index < designationCount; index++)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:perf/f4-designate-{index:D4}"),
                ids[index],
                date,
                new CharacterSuccessionActionCommandPayload(
                    new DesignateHeirAction(ids[designationCount + index], null)));
            Assert.True(simulation.Submit(command).IsValid);
        }

        Stopwatch workflow = Stopwatch.StartNew();
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch query = Stopwatch.StartNew();
        int currentCount = Enumerable.Range(0, designationCount).Count(index =>
            simulation.World.CharacterSuccessions.TryGetCurrentDesignation(
                ids[index],
                out HeirDesignationState? designation)
            && designation.HeirCharacterId == ids[designationCount + index]);
        query.Stop();
        Stopwatch snapshotChecksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
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

        snapshotChecksum.Stop();

        Assert.Equal(designationCount, events.Count);
        Assert.All(events, item => Assert.IsType<HeirDesignatedOutcome>(
            Assert.IsType<CharacterSuccessionActionResolvedEventPayload>(
                item.Payload).Outcome));
        Assert.Equal(designationCount, currentCount);
        Assert.Equal(characterCount, snapshot.Characters.CharacterStates.Count);
        Assert.Equal(designationCount, snapshot.CharacterSuccessions.Designations.Count);
        Assert.Empty(snapshot.CharacterSuccessions.History);
        output.WriteLine(
            $"f4_heir_designation_raw characters={characterCount}; designations={designationCount}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={snapshotChecksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksum.Value}");
    }

    [Fact]
    public void F714_ThousandCharacterFiveHundredClaimFixtureRecordsRawLocalPerformance()
    {
        const int characterCount = 1_000;
        const int claimCount = 500;
        CampaignDate date = new(200, 5, 10);
        EntityId[] ids = Enumerable.Range(0, characterCount)
            .Select(index => new EntityId($"character:perf/f7-{index:D4}"))
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
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            ids.Select(id => new CharacterState(
                CharacterContractVersions.State,
                id,
                [],
                [],
                CharacterConditionState.Default,
                [])).ToArray(),
            [],
            []);
        CampaignSimulation simulation = new(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters));
        for (int index = 0; index < claimCount; index++)
        {
            CampaignCommand command = CampaignCommand.Create(
                new EntityId($"command:perf/f7-claim-{index:D4}"),
                ids[index],
                date,
                new CharacterSuccessionClaimActionCommandPayload(
                    new AssertSuccessionClaimAction(ids[claimCount + index])));
            Assert.True(simulation.Submit(command).IsValid);
        }

        Stopwatch workflow = Stopwatch.StartNew();
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch query = Stopwatch.StartNew();
        int activeCount = Enumerable.Range(0, claimCount).Count(index =>
            simulation.World.CharacterSuccessions.TryGetActiveClaim(
                ids[claimCount + index],
                ids[index],
                out SuccessionClaimState? claim)
            && claim.Status == SuccessionClaimStatus.Active);
        query.Stop();
        Stopwatch snapshotChecksum = Stopwatch.StartNew();
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        SimulationChecksum checksum = SimulationChecksum.Compute(snapshot);
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

        snapshotChecksum.Stop();

        Assert.Equal(claimCount, events.Count);
        Assert.All(events, item => Assert.IsType<SuccessionClaimAssertedOutcome>(
            Assert.IsType<CharacterSuccessionClaimActionResolvedEventPayload>(
                item.Payload).Outcome));
        Assert.Equal(claimCount, activeCount);
        Assert.Equal(characterCount, snapshot.Characters.CharacterStates.Count);
        Assert.Equal(claimCount, snapshot.CharacterSuccessions.Claims.Count);
        Assert.Empty(snapshot.CharacterSuccessions.ClaimHistory);
        output.WriteLine(
            $"f7_succession_claim_raw characters={characterCount}; claims={claimCount}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={snapshotChecksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksum.Value}");
    }
}
