using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Core;

namespace Simulation.Core.Tests;

public sealed class SaveStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"three-kingdom-tests-{Guid.NewGuid():N}");

    public SaveStoreTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void SaveLoad_RoundTripsSnapshotAndChecksumExactly()
    {
        CampaignSimulation simulation = CreateSimulation();
        SaveEnvelope expected = CreateEnvelope(simulation);
        string path = Path.Combine(directory, "campaign.save.gz");

        new SaveStore().SaveAtomic(path, expected);
        SaveEnvelope actual = new SaveStore().Load(path, expected.ContentManifests);

        Assert.Equal(expected.Checksum, actual.Checksum);
        Assert.Equal(
            SimulationChecksum.Compute(expected.Snapshot),
            SimulationChecksum.Compute(actual.Snapshot));
        Assert.Equal(expected.Seed, actual.Seed);
    }

    [Fact]
    public void InterruptedAtomicWrite_PreservesLastValidSave()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "campaign.save.gz");
        SaveEnvelope original = CreateEnvelope(CreateSimulation());
        store.SaveAtomic(path, original);
        byte[] originalBytes = File.ReadAllBytes(path);

        CampaignSimulation changed = CreateSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:changed"),
            changed.World.Entities[0].Id,
            changed.World.Calendar.Date,
            new AdjustResourcesCommandPayload(changed.World.Entities[0].Id, 0, 500, 0));
        Assert.True(changed.Submit(command).IsValid);
        changed.ResolveTurn();
        SaveEnvelope replacement = CreateEnvelope(changed);

        Assert.Throws<SimulatedInterruptionException>(() =>
            store.SaveAtomic(path, replacement, _ => throw new SimulatedInterruptionException()));

        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(original.Checksum, store.Load(path).Checksum);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void CorruptPrimary_RemainsUntouchedAndRecoversNewestValidGeneration()
    {
        SaveStore store = new();
        string path = Path.Combine(directory, "autosave.save.gz");
        SaveEnvelope first = CreateEnvelope(CreateSimulation());
        store.SaveAutosave(path, first);

        CampaignSimulation changed = CreateSimulation();
        CampaignCommand command = CampaignCommand.Create(
            new EntityId("command:autosave/second"),
            changed.World.Entities[0].Id,
            changed.World.Calendar.Date,
            new AdjustResourcesCommandPayload(changed.World.Entities[0].Id, 0, 1, 0));
        Assert.True(changed.Submit(command).IsValid);
        changed.ResolveTurn();
        store.SaveAutosave(path, CreateEnvelope(changed));

        byte[] corrupt = [0x00, 0x01, 0x02, 0x03];
        File.WriteAllBytes(path, corrupt);
        SaveLoadResult recovered = store.LoadWithRecovery(path);

        Assert.Equal(first.Checksum, recovered.Envelope.Checksum);
        Assert.Equal(path + ".1", recovered.SourcePath);
        Assert.NotNull(recovered.RecoveryDiagnostic);
        Assert.Equal(corrupt, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaOne_MigratesForwardWithoutOverwritingSource()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject schemaOne = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        schemaOne["schemaVersion"] = 1;
        schemaOne.Remove("diagnosticEvents");
        JsonObject schemaOneSnapshot = schemaOne["snapshot"]!.AsObject();
        schemaOneSnapshot.Remove("geography");
        JsonArray schemaOneVersions = schemaOneSnapshot["systemVersions"]!.AsArray();
        JsonNode schemaOneGeographyVersion = schemaOneVersions.Single(
            node => node!["systemId"]!.GetValue<string>() == "simulation.geography")!;
        schemaOneVersions.Remove(schemaOneGeographyVersion);
        string path = Path.Combine(directory, "schema-one.save.gz");
        WriteJsonGzip(path, schemaOne);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.DiagnosticEvents);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SchemaTwo_MigratesEmptyGeographyWithoutOverwritingSource()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject schemaTwo = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        schemaTwo["schemaVersion"] = 2;
        JsonObject snapshot = schemaTwo["snapshot"]!.AsObject();
        snapshot.Remove("geography");
        JsonArray versions = snapshot["systemVersions"]!.AsArray();
        JsonNode geographyVersion = versions.Single(node => node!["systemId"]!.GetValue<string>() == "simulation.geography")!;
        versions.Remove(geographyVersion);
        string path = Path.Combine(directory, "schema-two.save.gz");
        WriteJsonGzip(path, schemaTwo);
        byte[] sourceBytes = File.ReadAllBytes(path);

        SaveEnvelope migrated = new SaveStore().Load(path);

        Assert.Equal(SaveEnvelope.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Empty(migrated.Snapshot.Geography.Graph.Routes);
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedMigration_DoesNotOverwriteSource()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject invalid = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        invalid["schemaVersion"] = 1;
        string path = Path.Combine(directory, "invalid-schema-one.save.gz");
        WriteJsonGzip(path, invalid);
        byte[] sourceBytes = File.ReadAllBytes(path);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void MissingRequiredManifest_BlocksLoadWithPreciseList()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation());
        string path = Path.Combine(directory, "content.save.gz");
        new SaveStore().SaveAtomic(path, envelope);

        SaveCompatibilityException exception = Assert.Throws<SaveCompatibilityException>(() =>
            new SaveStore().Load(path, []));

        Assert.Contains("base:synthetic@1.0.0 (sha256:abc)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOptionalPresentationManifest_MayBeSubstituted()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation()) with
        {
            ContentManifests =
            [
                new(new EntityId("presentation:portraits"), "1.0.0", "sha256:optional", false),
            ],
        };
        string path = Path.Combine(directory, "optional-content.save.gz");
        new SaveStore().SaveAtomic(path, envelope);

        SaveEnvelope loaded = new SaveStore().Load(path, []);

        Assert.Equal(envelope.Checksum, loaded.Checksum);
    }

    [Fact]
    public void UnknownRequiredEnvelopeData_IsNotSilentlyDiscarded()
    {
        SaveEnvelope current = CreateEnvelope(CreateSimulation());
        JsonObject json = JsonSerializer.SerializeToNode(current, CanonicalJson.Options)!.AsObject();
        json["futureRequiredState"] = new JsonObject { ["value"] = 42 };
        string path = Path.Combine(directory, "future-data.save.gz");
        WriteJsonGzip(path, json);

        Assert.Throws<SaveCompatibilityException>(() => new SaveStore().Load(path));
    }

    [Fact]
    public void IncompatibleSnapshotSystemVersion_BlocksSave()
    {
        SaveEnvelope envelope = CreateEnvelope(CreateSimulation());
        SaveEnvelope incompatible = envelope with
        {
            Snapshot = envelope.Snapshot with
            {
                SystemVersions = [new SystemVersion("simulation.calendar", 999)],
            },
        };
        incompatible = incompatible with
        {
            Checksum = SimulationChecksum.Compute(incompatible.Snapshot).Value,
        };

        Assert.Throws<SaveCompatibilityException>(() =>
            new SaveStore().SaveAtomic(Path.Combine(directory, "incompatible.save.gz"), incompatible));
    }

    [Fact]
    public void DiagnosticHistory_IsBounded()
    {
        CampaignSimulation simulation = CreateSimulation();
        for (int index = 0; index < 300; index++)
        {
            CampaignCommand invalid = CampaignCommand.Create(
                new EntityId($"command:invalid/{index:D4}"),
                new EntityId("actor:missing"),
                simulation.World.Calendar.Date,
                new ChangeSimulationTierCommandPayload(simulation.World.Entities[0].Id, SimulationTier.Full));
            Assert.False(simulation.Submit(invalid).IsValid);
        }

        SaveEnvelope envelope = CreateEnvelope(simulation);

        Assert.Equal(256, envelope.DiagnosticCommands.Count);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    private static CampaignSimulation CreateSimulation() => new(SyntheticSimulation.CreateWorld(5, 99));

    private static SaveEnvelope CreateEnvelope(CampaignSimulation simulation) => SaveEnvelope.Create(
        "0.1.0",
        [new ContentManifestReference(new EntityId("base:synthetic"), "1.0.0", "sha256:abc", true)],
        simulation,
        DateTimeOffset.Parse("2026-07-12T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static void WriteJsonGzip(string path, JsonObject json)
    {
        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(gzip, json, CanonicalJson.Options);
    }

    private sealed class SimulatedInterruptionException : Exception;
}
