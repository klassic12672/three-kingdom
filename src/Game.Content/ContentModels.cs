using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Simulation.Core;

namespace Game.Content;

public static class ContentContractVersions
{
    public const int Manifest = 1;
    public const int Record = 1;
    public const int Localization = 1;
    public const int SourceReference = 1;
    public const int AssetProvenance = 1;
}

public enum ContentTag
{
    Historical,
    Disputed,
    Inferred,
    Romance,
    Fictional,
}

public enum ContentClassification
{
    General,
    NonExplicitRelationship,
    SexuallyExplicit,
}

public enum ContentFileKind
{
    Records,
    Overrides,
    Localization,
    Glossary,
    Sources,
    Provenance,
}

public enum LocalizationReviewState
{
    Draft,
    Reviewed,
    Approved,
}

public enum AssetOrigin
{
    Human,
    OfflineAi,
    LiveGenerated,
}

public sealed record ContentDependency(
    [property: JsonRequired] EntityId PackId,
    [property: JsonRequired] string VersionRequirement,
    [property: JsonRequired] bool Required);

public sealed record ContentFile(
    [property: JsonRequired] string Path,
    [property: JsonRequired] ContentFileKind Kind,
    [property: JsonRequired] string Sha256);

public sealed record ProvenanceSummary(
    [property: JsonRequired] string License,
    [property: JsonRequired] string RightsHolder,
    [property: JsonRequired] string SourceRegister,
    [property: JsonRequired] string AssetRegister);

public sealed record ContentManifest(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] EntityId PackId,
    [property: JsonRequired] string Version,
    [property: JsonRequired] string MinimumGameVersion,
    [property: JsonRequired] int ContentSchemaVersion,
    [property: JsonRequired] bool IsBuiltIn,
    [property: JsonRequired] bool ReleaseEligible,
    [property: JsonRequired] IReadOnlyList<ContentDependency> Dependencies,
    [property: JsonRequired] int LoadPriority,
    [property: JsonRequired] IReadOnlyList<ContentFile> Files,
    [property: JsonRequired] IReadOnlyList<string> Authors,
    [property: JsonRequired] ProvenanceSummary Provenance,
    [property: JsonRequired] string Checksum);

public sealed record ContentRecord(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] EntityId Id,
    [property: JsonRequired] string RecordType,
    [property: JsonRequired] ContentTag ContentTag,
    [property: JsonRequired] ContentClassification Classification,
    [property: JsonRequired] IReadOnlyList<EntityId> SourceIds,
    [property: JsonRequired] IReadOnlyList<EntityId> LocalizationKeys,
    [property: JsonRequired] bool ReleaseMarked,
    [property: JsonRequired] JsonObject Data);

public sealed record ContentRecordDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] IReadOnlyList<ContentRecord> Records);

public sealed record FieldOverride(
    [property: JsonRequired] string JsonPath,
    [property: JsonRequired] JsonNode? Value);

public sealed record ContentOverride(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] EntityId TargetId,
    [property: JsonRequired] IReadOnlyList<FieldOverride> Fields);

public sealed record ContentOverrideDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] IReadOnlyList<ContentOverride> Overrides);

public sealed record LocalizationEntry(
    int SchemaVersion,
    EntityId Key,
    string Locale,
    string Text,
    string Context,
    IReadOnlyList<string> Variables,
    LocalizationReviewState ReviewState,
    IReadOnlyList<EntityId> SourceContentIds,
    bool ReleaseMarked);

public sealed record GlossaryEntry(
    EntityId TermId,
    string Korean,
    string English,
    string Notes,
    LocalizationReviewState ReviewState);

public sealed record SourceReference(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] EntityId SourceId,
    [property: JsonRequired] string Claim,
    [property: JsonRequired] string Location,
    [property: JsonRequired] string Confidence,
    [property: JsonRequired] string Notes,
    [property: JsonRequired] string Citation,
    [property: JsonRequired] string SourceTier);

public sealed record SourceReferenceDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] IReadOnlyList<SourceReference> Sources);

public sealed record AssetProvenance(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] EntityId AssetId,
    [property: JsonRequired] IReadOnlyList<EntityId> ContentIds,
    [property: JsonRequired] AssetOrigin Origin,
    [property: JsonRequired] string RightsStatus,
    [property: JsonRequired] string? ModelServiceVersion,
    [property: JsonRequired] string? GenerationDate,
    [property: JsonRequired] string? InputSources,
    [property: JsonRequired] string? PromptBrief,
    [property: JsonRequired] string HumanEdits,
    [property: JsonRequired] string Reviewer,
    [property: JsonRequired] string CommercialRightsEvidence,
    [property: JsonRequired] string Sha256,
    [property: JsonRequired] bool HumanApproved,
    [property: JsonRequired] bool ReleaseEligible,
    [property: JsonRequired] ContentClassification Classification);

public sealed record AssetProvenanceDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] IReadOnlyList<AssetProvenance> Assets);

public sealed record LoadedContentPack(ContentManifest Manifest, string ManifestPath, string Checksum);

public sealed record NormalizedContentRecord(
    EntityId Id,
    string RecordType,
    ContentTag ContentTag,
    ContentClassification Classification,
    IReadOnlyList<EntityId> SourceIds,
    IReadOnlyList<EntityId> LocalizationKeys,
    bool ReleaseMarked,
    EntityId OwningPackId,
    IReadOnlyList<EntityId> AppliedOverridePackIds,
    System.Text.Json.JsonElement Data);

internal sealed record ContentRecordLineage(
    EntityId OwningPackId,
    IReadOnlyList<EntityId> AppliedOverridePackIds)
{
    public ContentRecordLineage AddOverride(EntityId packId) => this with
    {
        AppliedOverridePackIds = AppliedOverridePackIds
            .Append(packId)
            .Distinct()
            .Order()
            .ToArray(),
    };
}
