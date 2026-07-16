using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace Simulation.Core.Tests;

public sealed class CharacterSuccessionResolutionPerformanceTests
{
    private readonly ITestOutputHelper output;

    public CharacterSuccessionResolutionPerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void F915_ThousandCharacterHundredResolutionFixtureRecordsRawLocalPerformance()
    {
        const int characterCount = 1_000;
        const int resolutionCount = 100;
        CampaignDate date = new(200, 5, 10);
        EntityId[] ids = Enumerable.Range(0, characterCount)
            .Select(index => new EntityId($"character:perf/f9-{index:D4}"))
            .ToArray();
        CharacterDefinition[] definitions = ids.Select((id, index) =>
        {
            EntityId nameKey = new($"loc:{id.Value.Replace(':', '/')}");
            return new CharacterDefinition(
                CharacterContractVersions.Definition,
                id,
                nameKey,
                index < resolutionCount
                    ? new CampaignDate(160, 1, 1)
                    : index < resolutionCount * 2
                        ? new CampaignDate(180, 1, 1)
                        : new CampaignDate(170, 1, 1),
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
        CharacterState[] states = ids.Select((id, index) =>
        {
            CharacterParentLink[] parents = index is >= resolutionCount
                    and < resolutionCount * 2
                ? [new(ids[index - resolutionCount], ParentChildLinkKind.Biological)]
                : [];
            return new CharacterState(
                CharacterContractVersions.State,
                id,
                parents.Select(item => item.ParentCharacterId).ToArray(),
                parents,
                CharacterConditionState.Default,
                []);
        }).ToArray();
        CharacterWorldSnapshot characters = new(
            CharacterContractVersions.Snapshot,
            [],
            definitions,
            [],
            [],
            states,
            [],
            []);
        CharacterResourceWorldSnapshot resources = new(
            CharacterResourceContractVersions.Snapshot,
            Enumerable.Range(0, resolutionCount * 2)
                .Select(index => new CharacterWealthAccountState(
                    CharacterResourceContractVersions.State,
                    CharacterResourceIds.DeriveWealthAccountId(ids[index]),
                    ids[index],
                    index < resolutionCount ? 10 : 1))
                .ToArray(),
            [],
            []);
        CharacterEstateHoldingWorldSnapshot estates = new(
            CharacterEstateHoldingContractVersions.Snapshot,
            Enumerable.Range(0, resolutionCount)
                .Select(index => new CharacterEstateHoldingState(
                    CharacterEstateHoldingContractVersions.State,
                    new($"estate:perf/f9-{index:D4}"),
                    ids[index]))
                .ToArray());
        CampaignSimulation simulation = new(WorldState.Create(
            date,
            20260716,
            [],
            GeographicWorldSnapshot.Empty,
            characters,
            RelationshipWorldSnapshot.Empty,
            CareerWorldSnapshot.Empty,
            resources,
            estates,
            CharacterMarriageWorldSnapshot.Empty,
            CharacterGuardianshipWorldSnapshot.Empty,
            CharacterPregnancyWorldSnapshot.Empty,
            CharacterSuccessionWorldSnapshot.Empty));
        SuccessionResolutionRule rule = new(
            CharacterSuccessionContractVersions.ResolutionRule,
            new(
                CharacterSuccessionContractVersions.CandidateEligibilityRule,
                [SuccessionCandidateBasis.BiologicalDescendant],
                2,
                0,
                AllowsIncapacitatedCandidates: false,
                Enum.GetValues<CharacterCustodyStatus>()),
            [SuccessionLegalBasis.BiologicalDescendant],
            IncludesPrincipalSpouse: false,
            AllowedCollateralKinds: [],
            MaximumCollateralDistance: 0,
            SuccessionContestResolutionMode.ResolveByStableId,
            MaximumCandidates: 8,
            MaximumDisputedCandidates: 4,
            CreatesRegencyForIncapacitatedSuccessor: false,
            SuccessionNoAcceptedSuccessorBehavior.EndCampaign);
        Stopwatch preparation = Stopwatch.StartNew();
        for (int index = 0; index < resolutionCount; index++)
        {
            EntityId subject = ids[index];
            EntityId commandId =
                new($"command:perf/f9-resolution-{index:D4}");
            EntityId expected =
                simulation.World.GetCharacterSuccessionResolutionStateId(
                    subject,
                    rule,
                    date,
                    simulation.World.Calendar.TurnIndex);
            CampaignCommand command = CampaignCommand.Create(
                commandId,
                CharacterConditionSystem.AuthoritativeActorId,
                date,
                new CharacterConditionActionCommandPayload(
                    new ResolveCharacterSuccessionDeathAction(
                        subject,
                        simulation.World.Characters.Profiles.Single(
                            item => item.CharacterId == subject).Condition,
                        rule,
                        expected,
                        null,
                        null,
                        null)));
            Assert.True(simulation.Submit(command).IsValid);
        }
        preparation.Stop();

        Stopwatch workflow = Stopwatch.StartNew();
        IReadOnlyList<CampaignEvent> events = simulation.ResolveTurn();
        workflow.Stop();
        Stopwatch query = Stopwatch.StartNew();
        long inheritedWealth = Enumerable.Range(0, resolutionCount)
            .Sum(index => simulation.World.CharacterResources.GetWealth(
                ids[resolutionCount + index]));
        int inheritedEstates =
            simulation.World.CharacterEstateHoldings.Holdings.Count(
                item => item.OwnerCharacterId.Value.StartsWith(
                    "character:perf/f9-01",
                    StringComparison.Ordinal));
        int resolutions = simulation.World.CharacterSuccessions.Resolutions.Count;
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

        Assert.Equal(resolutionCount, events.Count);
        Assert.All(events, item => Assert.IsType<
            CharacterSuccessionDeathResolvedOutcome>(Assert.IsType<
                CharacterConditionActionResolvedEventPayload>(item.Payload)
                .Outcome));
        Assert.Equal(1_100, inheritedWealth);
        Assert.Equal(resolutionCount, inheritedEstates);
        Assert.Equal(resolutionCount, resolutions);
        output.WriteLine(
            $"f9_succession_resolution_raw characters={characterCount}; "
            + $"resolutions={resolutionCount}; "
            + $"prepare_submit_ms={preparation.Elapsed.TotalMilliseconds:F3}; "
            + $"workflow_ms={workflow.Elapsed.TotalMilliseconds:F3}; "
            + $"query_ms={query.Elapsed.TotalMilliseconds:F3}; "
            + $"snapshot_checksum_ms={snapshotChecksum.Elapsed.TotalMilliseconds:F3}; "
            + $"json_bytes={json.Length}; gzip_bytes={compressed.Length}; "
            + $"checksum={checksum.Value}");
    }
}
