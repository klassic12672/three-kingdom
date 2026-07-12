using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

public sealed class ContentChecksumTests
{
    [Fact]
    public void PackChecksumUsesPlatformIndependentCanonicalForm()
    {
        ContentManifest manifest = new(
            1,
            new EntityId("pack:canonical"),
            "1.2.3",
            "0.1.0",
            1,
            false,
            true,
            [
                new ContentDependency(new EntityId("pack:b"), "2.0.0", false),
                new ContentDependency(new EntityId("pack:a"), ">=1.0.0", true),
            ],
            7,
            [
                new ContentFile("nested\\z.csv", ContentFileKind.Localization, "zzz"),
                new ContentFile("a.json", ContentFileKind.Records, "aaa"),
            ],
            ["Zoe", "Alice"],
            new ProvenanceSummary("CC0", "Studio", "sources.json", "assets.json"),
            string.Empty);

        Assert.Equal(
            "c6ab2096629b9bb62ccd9de3fbe1f77295d01db5558cde7c2808081ec7627f1f",
            ContentChecksum.ComputePack(manifest));
    }
}
