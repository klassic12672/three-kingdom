using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

public sealed class SaveContentCompatibilityTests
{
    [Fact]
    public void AddedPackIsCompatibleWhileRemovedOrChangedRequiredPackBlocksLoad()
    {
        string repositoryRoot = FindRepositoryRoot();
        ContentRegistry registry = new ContentPackLoader()
            .LoadRepository(Path.Combine(repositoryRoot, "data"), "0.1.0")
            .Registry;
        CampaignSimulation simulation = new(SyntheticSimulation.CreateWorld(2, 7));
        SaveEnvelope envelope = SaveEnvelope.Create("0.1.0", registry.ToSaveManifestReferences(), simulation);
        string path = Path.Combine(Path.GetTempPath(), $"content-save-{Guid.NewGuid():N}.save.gz");

        try
        {
            SaveStore store = new();
            store.SaveAtomic(path, envelope);
            ContentManifestReference added = new(new EntityId("mod:added"), "1.0.0", new string('b', 64), true);
            Assert.Equal(envelope.Checksum, store.Load(path, registry.ToSaveManifestReferences().Append(added)).Checksum);

            SaveCompatibilityException removed = Assert.Throws<SaveCompatibilityException>(() => store.Load(path, []));
            Assert.Contains("core:base", removed.Message, StringComparison.Ordinal);

            ContentManifestReference changed = registry.ToSaveManifestReferences()[0] with { Checksum = new string('c', 64) };
            Assert.Throws<SaveCompatibilityException>(() => store.Load(path, [changed]));

            ContentManifestReference olderVersion = registry.ToSaveManifestReferences()[0] with { Version = "0.1.0" };
            Assert.Throws<SaveCompatibilityException>(() => store.Load(path, [olderVersion]));
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
