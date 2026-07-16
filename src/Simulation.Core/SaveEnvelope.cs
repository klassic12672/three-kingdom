namespace Simulation.Core;

public sealed record ContentManifestReference(
    EntityId PackId,
    string Version,
    string Checksum,
    bool RequiredForSimulation);

public sealed record SaveEnvelope(
    int SchemaVersion,
    int ContractVersion,
    string GameVersion,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<ContentManifestReference> ContentManifests,
    ulong Seed,
    WorldSnapshot Snapshot,
    IReadOnlyList<CampaignCommand> DiagnosticCommands,
    IReadOnlyList<CampaignEvent> DiagnosticEvents,
    string Checksum)
{
    public const int CurrentSchemaVersion = 29;

    public static SaveEnvelope Create(
        string gameVersion,
        IEnumerable<ContentManifestReference> contentManifests,
        CampaignSimulation simulation,
        DateTimeOffset? createdUtc = null)
    {
        WorldSnapshot snapshot = simulation.World.CaptureSnapshot();
        return new SaveEnvelope(
            CurrentSchemaVersion,
            ContractVersions.SaveEnvelope,
            gameVersion,
            createdUtc ?? DateTimeOffset.UtcNow,
            contentManifests.OrderBy(item => item.PackId).ToArray(),
            snapshot.RootSeed,
            snapshot,
            simulation.RecentCommands.TakeLast(256).ToArray(),
            simulation.RecentEvents.TakeLast(256).ToArray(),
            SimulationChecksum.Compute(snapshot).Value);
    }
}

public sealed record SaveLoadResult(SaveEnvelope Envelope, string SourcePath, string? RecoveryDiagnostic);
