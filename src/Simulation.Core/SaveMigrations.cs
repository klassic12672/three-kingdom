using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core;

public interface ISaveMigration
{
    int FromSchemaVersion { get; }

    int ToSchemaVersion { get; }

    JsonObject Migrate(JsonObject source);
}

public sealed class SaveSchemaRegistry
{
    private readonly IReadOnlyDictionary<int, ISaveMigration> migrations;

    public SaveSchemaRegistry(IEnumerable<ISaveMigration>? migrations = null)
    {
        ISaveMigration[] registered = (migrations ?? [new SaveMigrationV1ToV2(), new SaveMigrationV2ToV3()]).ToArray();
        if (registered.Any(item => item.ToSchemaVersion != item.FromSchemaVersion + 1))
        {
            throw new ArgumentException("Save migrations must advance exactly one schema version.", nameof(migrations));
        }

        this.migrations = registered.ToDictionary(item => item.FromSchemaVersion);
    }

    public JsonObject MigrateToCurrent(JsonObject source)
    {
        JsonObject working = (JsonObject)source.DeepClone();
        int schemaVersion = GetSchemaVersion(working);
        if (schemaVersion > SaveEnvelope.CurrentSchemaVersion)
        {
            throw new SaveCompatibilityException(
                $"Save schema {schemaVersion} is newer than supported schema {SaveEnvelope.CurrentSchemaVersion}.");
        }

        while (schemaVersion < SaveEnvelope.CurrentSchemaVersion)
        {
            if (!migrations.TryGetValue(schemaVersion, out ISaveMigration? migration))
            {
                throw new SaveCompatibilityException($"No migration is registered from save schema {schemaVersion}.");
            }

            try
            {
                working = migration.Migrate(working);
            }
            catch (Exception exception) when (exception is not SaveCompatibilityException)
            {
                throw new SaveCompatibilityException(
                    $"Migration from schema {migration.FromSchemaVersion} to {migration.ToSchemaVersion} failed.",
                    exception);
            }

            schemaVersion = GetSchemaVersion(working);
            if (schemaVersion != migration.ToSchemaVersion)
            {
                throw new SaveCompatibilityException(
                    $"Migration from schema {migration.FromSchemaVersion} did not produce schema {migration.ToSchemaVersion}.");
            }
        }

        return working;
    }

    private static int GetSchemaVersion(JsonObject source) => source["schemaVersion"]?.GetValue<int>()
        ?? throw new SaveCompatibilityException("Save is missing required 'schemaVersion' data.");
}

public sealed class SaveMigrationV1ToV2 : ISaveMigration
{
    public int FromSchemaVersion => 1;

    public int ToSchemaVersion => 2;

    public JsonObject Migrate(JsonObject source)
    {
        if (source.ContainsKey("diagnosticEvents"))
        {
            throw new SaveCompatibilityException("Schema 1 unexpectedly contains schema 2 diagnostic event data.");
        }

        source["diagnosticEvents"] = new JsonArray();
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}

public sealed class SaveMigrationV2ToV3 : ISaveMigration
{
    public int FromSchemaVersion => 2;

    public int ToSchemaVersion => 3;

    public JsonObject Migrate(JsonObject source)
    {
        if (source["snapshot"] is not JsonObject snapshot
            || snapshot["systemVersions"] is not JsonArray systemVersions)
        {
            throw new SaveCompatibilityException("Schema 2 save is missing snapshot system versions.");
        }

        if (snapshot.ContainsKey("geography")
            || systemVersions.Any(node => node?["systemId"]?.GetValue<string>() == "simulation.geography"))
        {
            throw new SaveCompatibilityException("Schema 2 unexpectedly contains schema 3 geography data.");
        }

        snapshot["geography"] = System.Text.Json.JsonSerializer.SerializeToNode(
            GeographicWorldSnapshot.Empty,
            SimulationJson.CreateOptions());
        systemVersions.Add(new JsonObject
        {
            ["systemId"] = "simulation.geography",
            ["version"] = 1,
        });
        WorldSnapshot migratedSnapshot = snapshot.Deserialize<WorldSnapshot>(SimulationJson.CreateOptions())
            ?? throw new SaveCompatibilityException("Migrated geography snapshot is empty.");
        source["checksum"] = SimulationChecksum.Compute(migratedSnapshot).Value;
        source["schemaVersion"] = ToSchemaVersion;
        return source;
    }
}
