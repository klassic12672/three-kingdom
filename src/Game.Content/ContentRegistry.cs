using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Simulation.Core;

namespace Game.Content;

public sealed class ContentRegistry
{
    private readonly FrozenDictionary<EntityId, NormalizedContentRecord> records;
    private readonly FrozenDictionary<(EntityId Key, string Locale), LocalizationEntry> localization;
    private readonly FrozenDictionary<EntityId, GlossaryEntry> glossary;

    internal ContentRegistry(
        IEnumerable<ContentRecord> records,
        IReadOnlyDictionary<EntityId, ContentRecordLineage> recordLineage,
        IEnumerable<LocalizationEntry> localization,
        IEnumerable<GlossaryEntry> glossary,
        IEnumerable<SourceReference> sources,
        IEnumerable<AssetProvenance> assets,
        IEnumerable<LoadedContentPack> packs)
    {
        this.records = records
            .Select(record => new NormalizedContentRecord(
                record.Id,
                record.RecordType,
                record.ContentTag,
                record.Classification,
                record.SourceIds.ToArray(),
                record.LocalizationKeys.ToArray(),
                record.ReleaseMarked,
                recordLineage.TryGetValue(record.Id, out ContentRecordLineage? lineage)
                    ? lineage.OwningPackId
                    : throw new InvalidDataException($"Content record '{record.Id}' has no owning-pack lineage."),
                lineage.AppliedOverridePackIds.Order().ToArray(),
                System.Text.Json.JsonSerializer.SerializeToElement(record.Data, ContentJson.CreateOptions())))
            .ToFrozenDictionary(record => record.Id);
        this.localization = localization.ToFrozenDictionary(entry => (entry.Key, entry.Locale));
        this.glossary = glossary.ToFrozenDictionary(entry => entry.TermId);
        Sources = sources.OrderBy(source => source.SourceId).ToArray();
        Assets = assets.OrderBy(asset => asset.AssetId).ToArray();
        Packs = packs.ToArray();
        Checksum = ContentChecksum.ComputeRegistry(Packs);
    }

    public IReadOnlyList<LoadedContentPack> Packs { get; }

    public IReadOnlyList<SourceReference> Sources { get; }

    public IReadOnlyList<AssetProvenance> Assets { get; }

    public string Checksum { get; }

    public int RecordCount => records.Count;

    public int LocalizationCount => localization.Count;

    public int GlossaryCount => glossary.Count;

    public bool TryGet(EntityId id, [NotNullWhen(true)] out NormalizedContentRecord? record) => records.TryGetValue(id, out record);

    public bool TryGetText(EntityId key, string locale, out string? text)
    {
        if (localization.TryGetValue((key, locale), out LocalizationEntry? entry))
        {
            text = entry.Text;
            return true;
        }

        text = null;
        return false;
    }

    public IReadOnlyList<NormalizedContentRecord> Records => records.Values.OrderBy(record => record.Id).ToArray();

    public IReadOnlyList<LocalizationEntry> LocalizationEntries => localization.Values
        .OrderBy(entry => entry.Key)
        .ThenBy(entry => entry.Locale, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<GlossaryEntry> GlossaryEntries => glossary.Values.OrderBy(entry => entry.TermId).ToArray();

    public IReadOnlyList<ContentManifestReference> ToSaveManifestReferences() => Packs
        .Select(pack => new ContentManifestReference(
            pack.Manifest.PackId,
            pack.Manifest.Version,
            pack.Checksum,
            RequiredForSimulation: true))
        .ToArray();

}

public sealed record ContentLoadResult(
    ContentRegistry Registry,
    ContentValidationReport Report,
    IReadOnlyList<LoadedContentPack> LoadOrder)
{
    public void ThrowIfInvalid()
    {
        if (Report.HasErrors)
        {
            throw new ContentValidationException(Report);
        }
    }
}
