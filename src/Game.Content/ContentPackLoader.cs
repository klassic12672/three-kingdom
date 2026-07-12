using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Core;

namespace Game.Content;

public sealed class ContentPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions();

    public ContentLoadResult LoadRepository(string dataRoot, string gameVersion)
    {
        string root = Path.GetFullPath(dataRoot);
        List<string> manifests = [];
        string builtIn = Path.Combine(root, "content-manifest.json");
        if (File.Exists(builtIn))
        {
            manifests.Add(builtIn);
        }

        string mods = Path.Combine(root, "mods");
        if (Directory.Exists(mods))
        {
            manifests.AddRange(Directory.EnumerateFiles(mods, "content-manifest.json", SearchOption.AllDirectories));
        }

        return Load(manifests, gameVersion, root);
    }

    public ContentLoadResult Load(
        IEnumerable<string> manifestPaths,
        string gameVersion,
        string? diagnosticRoot = null)
    {
        ContentValidationReport report = new();
        if (!SemanticVersion.TryParse(gameVersion, out SemanticVersion currentGameVersion))
        {
            throw new ArgumentException("Game version must use semantic-version syntax.", nameof(gameVersion));
        }

        PackDraft[] drafts = manifestPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => ReadPack(path, diagnosticRoot, currentGameVersion, report))
            .Where(draft => draft is not null)
            .Cast<PackDraft>()
            .ToArray();

        HashSet<EntityId> invalidPacks = ValidatePackIdentitiesAndDependencies(drafts, report);
        MarkAmbiguousOverrides(drafts, invalidPacks, report);
        IReadOnlyList<PackDraft> ordered = ResolveLoadOrder(drafts, invalidPacks, report);
        HashSet<EntityId> allRecordIds = ordered.SelectMany(draft => draft.Records).Select(record => record.Id).ToHashSet();
        HashSet<EntityId> allSourceIds = ordered.SelectMany(draft => draft.Sources).Select(source => source.SourceId).ToHashSet();
        HashSet<string> allLocalization = ordered.SelectMany(draft => draft.Localization).Select(LocalizationKey).ToHashSet(StringComparer.Ordinal);
        HashSet<EntityId> allProvenanceIds = ordered.SelectMany(draft => draft.Assets).Select(asset => asset.AssetId).ToHashSet();
        foreach (PackDraft draft in ordered)
        {
            ValidateDraftSemantics(
                draft,
                allRecordIds,
                allSourceIds,
                allLocalization,
                allProvenanceIds,
                report,
                invalidPacks);
        }

        PropagateInvalidDependencies(drafts, invalidPacks, report);
        ordered = ResolveLoadOrder(drafts, invalidPacks, report);
        Dictionary<EntityId, ContentRecord> records = [];
        Dictionary<string, LocalizationEntry> localization = new(StringComparer.Ordinal);
        Dictionary<EntityId, GlossaryEntry> glossary = [];
        Dictionary<EntityId, SourceReference> sources = [];
        Dictionary<EntityId, AssetProvenance> assets = [];
        List<LoadedContentPack> loadedPacks = [];

        foreach (PackDraft draft in ordered)
        {
            ContentDependency? unavailable = draft.Manifest.Dependencies.FirstOrDefault(
                dependency => dependency.Required
                    && loadedPacks.All(pack => pack.Manifest.PackId != dependency.PackId));
            if (unavailable is not null)
            {
                report.Error("dependency.invalid", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Required pack '{unavailable.PackId}' did not load.", "Repair the required pack before loading this dependent pack.");
                invalidPacks.Add(draft.Manifest.PackId);
                continue;
            }

            int before = report.ErrorCount;
            Dictionary<EntityId, ContentRecord> candidateRecords = new(records);
            Dictionary<string, LocalizationEntry> candidateLocalization = new(localization, StringComparer.Ordinal);
            Dictionary<EntityId, GlossaryEntry> candidateGlossary = new(glossary);
            Dictionary<EntityId, SourceReference> candidateSources = new(sources);
            Dictionary<EntityId, AssetProvenance> candidateAssets = new(assets);
            ApplyPack(draft, candidateRecords, candidateLocalization, candidateGlossary, candidateSources, candidateAssets, report);
            if (report.ErrorCount != before)
            {
                invalidPacks.Add(draft.Manifest.PackId);
                continue;
            }

            records = candidateRecords;
            localization = candidateLocalization;
            glossary = candidateGlossary;
            sources = candidateSources;
            assets = candidateAssets;
            loadedPacks.Add(new(draft.Manifest, draft.ManifestPath, draft.Checksum));
        }

        ValidateRegistry(records, localization, sources, assets, loadedPacks, report);
        ContentRegistry registry = new(records.Values, localization.Values, glossary.Values, sources.Values, assets.Values, loadedPacks);
        foreach (NormalizedContentRecord scenario in registry.Records.Where(record => record.RecordType == "geography_scenario"))
        {
            try
            {
                _ = new GeographicWorldState(GeographicContentLoader.LoadScenario(registry, scenario.Id));
            }
            catch (Exception exception) when (exception is InvalidDataException or SimulationValidationException or System.Text.Json.JsonException)
            {
                report.Error("geography.graph", "registry", "$.records", scenario.Id, exception.Message, "Repair geographic containment, route connectivity, scenario overlays, or typed map fields.");
            }
        }

        return new ContentLoadResult(registry, report, loadedPacks);
    }

    private static PackDraft? ReadPack(
        string manifestPath,
        string? diagnosticRoot,
        SemanticVersion gameVersion,
        ContentValidationReport report)
    {
        string displayManifest = DisplayPath(manifestPath, diagnosticRoot);
        int errorsBefore = report.ErrorCount;
        ContentManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ContentManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            report.Error("manifest.schema", displayManifest, "$", null, exception.Message, "Conform the manifest to data/schemas/content-manifest.schema.json.");
            return null;
        }

        if (manifest is null)
        {
            report.Error("manifest.empty", displayManifest, "$", null, "Manifest is empty.", "Provide a manifest JSON object.");
            return null;
        }

        if (manifest.Dependencies is null
            || manifest.Files is null
            || manifest.Authors is null
            || manifest.Provenance is null
            || manifest.Dependencies.Any(dependency => dependency is null)
            || manifest.Files.Any(file => file is null))
        {
            report.Error("manifest.required", displayManifest, "$", manifest.PackId.IsValid ? manifest.PackId : null, "Manifest is missing required arrays or provenance data.", "Conform the manifest to the published schema.");
            return null;
        }
        if (manifest.Dependencies.Any(dependency => !dependency.PackId.IsValid || string.IsNullOrWhiteSpace(dependency.VersionRequirement))
            || manifest.Files.Any(file => string.IsNullOrWhiteSpace(file.Path) || string.IsNullOrWhiteSpace(file.Sha256))
            || manifest.Authors.Any(string.IsNullOrWhiteSpace))
        {
            report.Error("manifest.required", displayManifest, "$", manifest.PackId.IsValid ? manifest.PackId : null, "Manifest contains empty dependency, file, or author fields.", "Populate every required manifest field.");
            return null;
        }

        bool valid = true;
        if (manifest.SchemaVersion != ContentContractVersions.Manifest
            || manifest.ContentSchemaVersion != ContentContractVersions.Record
            || !manifest.PackId.IsValid
            || !SemanticVersion.TryParse(manifest.Version, out _)
            || !SemanticVersion.TryParse(manifest.MinimumGameVersion, out SemanticVersion minimumGame))
        {
            report.Error("manifest.contract", displayManifest, "$", manifest.PackId.IsValid ? manifest.PackId : null, "Manifest contract, IDs, or versions are invalid.", "Use supported schema versions, namespaced IDs, and semantic versions.");
            valid = false;
        }
        else if (manifest.LoadPriority is < -100_000 or > 100_000
            || manifest.Dependencies.Select(dependency => dependency.PackId).Distinct().Count() != manifest.Dependencies.Count)
        {
            report.Error("manifest.range", displayManifest, "$", manifest.PackId, "Manifest priority or dependency list is invalid.", "Use priority -100000..100000 and unique dependency IDs.");
            valid = false;
        }
        else if (gameVersion.CompareTo(minimumGame) < 0)
        {
            report.Error("manifest.game_version", displayManifest, "$.minimumGameVersion", manifest.PackId, $"Pack requires game {minimumGame} or newer.", "Update the game or use a compatible pack version.");
            valid = false;
        }

        if (manifest.Authors.Count == 0
            || string.IsNullOrWhiteSpace(manifest.Provenance.License)
            || string.IsNullOrWhiteSpace(manifest.Provenance.RightsHolder))
        {
            report.Error("manifest.authorship", displayManifest, "$.authors", manifest.PackId, "Manifest authorship or provenance summary is incomplete.", "Record author, license, rights holder, and register paths.");
            valid = false;
        }

        string directory = Path.GetDirectoryName(manifestPath)!;
        List<ContentRecord> records = [];
        List<ContentOverride> overrides = [];
        List<LocalizationEntry> localization = [];
        List<GlossaryEntry> glossary = [];
        List<SourceReference> sources = [];
        List<AssetProvenance> assets = [];
        HashSet<string> filePaths = new(StringComparer.Ordinal);
        foreach (ContentFile file in manifest.Files.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            string relative = file.Path;
            if (!IsPortableContentPath(relative))
            {
                report.Error("manifest.file_path", displayManifest, "$.files", manifest.PackId, $"Content file path '{file.Path}' is not a portable relative path.", "Use ASCII path segments separated by forward slashes without '.' or '..' segments.");
                valid = false;
                continue;
            }

            string path = Path.GetFullPath(file.Path, directory);
            string display = DisplayPath(path, diagnosticRoot);
            if (Path.IsPathRooted(file.Path)
                || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !filePaths.Add(relative))
            {
                report.Error("manifest.file_path", displayManifest, "$.files", manifest.PackId, $"Content file path '{file.Path}' is unsafe or duplicated.", "Use one unique relative path within the pack directory.");
                valid = false;
                continue;
            }

            if (!File.Exists(path))
            {
                report.Error("manifest.file_missing", displayManifest, "$.files", manifest.PackId, $"Declared content file '{file.Path}' is missing.", "Restore the file or remove its declaration.");
                valid = false;
                continue;
            }

            string actualChecksum = ContentChecksum.ComputeFile(path);
            if (!StringComparer.Ordinal.Equals(actualChecksum, file.Sha256))
            {
                report.Error("manifest.file_checksum", displayManifest, "$.files", manifest.PackId, $"Checksum mismatch for '{file.Path}'.", $"Set sha256 to '{actualChecksum}' after reviewing the file.");
                valid = false;
            }

            try
            {
                switch (file.Kind)
                {
                    case ContentFileKind.Records:
                        ContentRecordDocument recordDocument = ReadJson<ContentRecordDocument>(path);
                        EnsureDocumentVersion(recordDocument.SchemaVersion, path);
                        records.AddRange(RequireItems(recordDocument.Records, path, "records"));
                        break;
                    case ContentFileKind.Overrides:
                        ContentOverrideDocument overrideDocument = ReadJson<ContentOverrideDocument>(path);
                        EnsureDocumentVersion(overrideDocument.SchemaVersion, path);
                        overrides.AddRange(RequireItems(overrideDocument.Overrides, path, "overrides"));
                        break;
                    case ContentFileKind.Localization:
                        localization.AddRange(Localization.ReadEntries(path, report, display));
                        break;
                    case ContentFileKind.Glossary:
                        glossary.AddRange(Localization.ReadGlossary(path, report, display));
                        break;
                    case ContentFileKind.Sources:
                        SourceReferenceDocument sourceDocument = ReadJson<SourceReferenceDocument>(path);
                        EnsureDocumentVersion(sourceDocument.SchemaVersion, path);
                        sources.AddRange(RequireItems(sourceDocument.Sources, path, "sources"));
                        break;
                    case ContentFileKind.Provenance:
                        AssetProvenanceDocument provenanceDocument = ReadJson<AssetProvenanceDocument>(path);
                        EnsureDocumentVersion(provenanceDocument.SchemaVersion, path);
                        assets.AddRange(RequireItems(provenanceDocument.Assets, path, "assets"));
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported content file kind '{file.Kind}'.");
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                report.Error("content.schema", display, "$", null, exception.Message, "Conform the file to its published JSON schema or CSV contract.");
                valid = false;
            }
        }

        if (overrides.Any(contentOverride => contentOverride.Fields is null
            || !contentOverride.TargetId.IsValid
            || contentOverride.Fields.Any(field => field is null || string.IsNullOrWhiteSpace(field.JsonPath))))
        {
            report.Error("override.contract", displayManifest, "$.files", manifest.PackId, "Override document contains missing targets or fields.", "Conform overrides to the published schema.");
            valid = false;
        }

        string checksum = ContentChecksum.ComputePack(manifest);
        if (!StringComparer.Ordinal.Equals(checksum, manifest.Checksum))
        {
            report.Error("manifest.pack_checksum", displayManifest, "$.checksum", manifest.PackId, "Pack checksum does not match canonical metadata and file checksums.", $"Set checksum to '{checksum}' after reviewing the pack.");
            valid = false;
        }

        valid &= report.ErrorCount == errorsBefore;

        return valid
            ? new PackDraft(manifest, displayManifest, checksum, records, overrides, localization, glossary, sources, assets)
            : null;
    }

    private static T ReadJson<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' is empty.");

    private static void EnsureDocumentVersion(int version, string path)
    {
        if (version != 1)
        {
            throw new InvalidDataException($"'{path}' uses unsupported document schema {version}.");
        }
    }

    private static IReadOnlyList<T> RequireItems<T>(
        IReadOnlyList<T>? items,
        string path,
        string property) where T : class
    {
        if (items is null || items.Any(item => item is null))
        {
            throw new InvalidDataException($"'{path}' must contain a non-null '{property}' array.");
        }

        return items;
    }

    private static HashSet<EntityId> ValidatePackIdentitiesAndDependencies(
        IReadOnlyList<PackDraft> drafts,
        ContentValidationReport report)
    {
        HashSet<EntityId> invalid = [];
        foreach (IGrouping<EntityId, PackDraft> duplicate in drafts.GroupBy(draft => draft.Manifest.PackId).Where(group => group.Count() > 1))
        {
            foreach (PackDraft draft in duplicate)
            {
                report.Error("manifest.duplicate_pack", draft.ManifestPath, "$.packId", draft.Manifest.PackId, "Pack ID is duplicated.", "Give every pack a unique stable ID.");
                invalid.Add(draft.Manifest.PackId);
            }
        }

        Dictionary<EntityId, PackDraft> byId = drafts
            .Where(draft => !invalid.Contains(draft.Manifest.PackId))
            .ToDictionary(draft => draft.Manifest.PackId);
        foreach (PackDraft draft in byId.Values)
        {
            foreach (ContentDependency dependency in draft.Manifest.Dependencies)
            {
                if (!byId.TryGetValue(dependency.PackId, out PackDraft? target))
                {
                    if (dependency.Required)
                    {
                        report.Error("dependency.missing", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Required pack '{dependency.PackId}' is missing.", "Install the required pack without modifying the save or built-in data.");
                        invalid.Add(draft.Manifest.PackId);
                    }
                    else
                    {
                        report.Warning("dependency.optional_missing", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Optional pack '{dependency.PackId}' is not installed.", "Install it only if the optional integration is wanted.");
                    }

                    continue;
                }

                try
                {
                    if (!SemanticVersion.Parse(target.Manifest.Version).Satisfies(dependency.VersionRequirement))
                    {
                        report.Error("dependency.version", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Pack '{dependency.PackId}' does not satisfy '{dependency.VersionRequirement}'.", "Install a compatible dependency version.");
                        invalid.Add(draft.Manifest.PackId);
                    }
                }
                catch (FormatException)
                {
                    report.Error("dependency.requirement", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Version requirement '{dependency.VersionRequirement}' is invalid.", "Use an exact semantic version or one comparator such as >=1.2.3.");
                    invalid.Add(draft.Manifest.PackId);
                }
            }

            if (draft.Manifest.IsBuiltIn
                && draft.Manifest.Dependencies.Any(dependency => byId.TryGetValue(dependency.PackId, out PackDraft? dependencyPack) && !dependencyPack.Manifest.IsBuiltIn))
            {
                report.Error("dependency.builtin_mod", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, "Built-in packs cannot depend on mods.", "Move the integration into a mod pack.");
                invalid.Add(draft.Manifest.PackId);
            }
        }

        PropagateInvalidDependencies(drafts, invalid, report);

        return invalid;
    }

    private static void PropagateInvalidDependencies(
        IReadOnlyList<PackDraft> drafts,
        HashSet<EntityId> invalid,
        ContentValidationReport report)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (PackDraft draft in drafts.Where(draft => !invalid.Contains(draft.Manifest.PackId)))
            {
                ContentDependency? unavailable = draft.Manifest.Dependencies
                    .FirstOrDefault(dependency => dependency.Required && invalid.Contains(dependency.PackId));
                if (unavailable is null)
                {
                    continue;
                }

                report.Error("dependency.invalid", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, $"Required pack '{unavailable.PackId}' is invalid.", "Repair or remove the invalid dependency before loading this pack.");
                invalid.Add(draft.Manifest.PackId);
                changed = true;
            }
        }
        while (changed);
    }

    private static void MarkAmbiguousOverrides(
        IReadOnlyList<PackDraft> drafts,
        HashSet<EntityId> invalid,
        ContentValidationReport report)
    {
        Dictionary<EntityId, PackDraft> validDrafts = drafts
            .Where(draft => !invalid.Contains(draft.Manifest.PackId))
            .ToDictionary(draft => draft.Manifest.PackId);
        var conflicts = drafts
            .Where(draft => !invalid.Contains(draft.Manifest.PackId))
            .SelectMany(draft => draft.Overrides.SelectMany(item => item.Fields.Select(field => new
            {
                draft,
                item.TargetId,
                field.JsonPath,
                draft.Manifest.LoadPriority,
            })))
            .GroupBy(item => (item.TargetId, item.JsonPath, item.LoadPriority))
            .Where(group =>
            {
                PackDraft[] participants = group.Select(item => item.draft).Distinct().ToArray();
                return participants.Length > 1
                    && participants.SelectMany(
                            (left, index) => participants.Skip(index + 1).Select(right => (left, right)))
                        .Any(pair => !DependsOn(pair.left, pair.right) && !DependsOn(pair.right, pair.left));
            });
        foreach (var conflict in conflicts)
        {
            foreach (PackDraft draft in conflict.Select(item => item.draft).Distinct())
            {
                report.Error(
                    "override.ambiguous",
                    draft.ManifestPath,
                    "$.files",
                    conflict.Key.TargetId,
                    $"Same-priority override conflict at '{conflict.Key.JsonPath}'.",
                    "Declare a dependency or distinct load priority so one override is authoritative.");
                invalid.Add(draft.Manifest.PackId);
            }
        }

        bool DependsOn(PackDraft dependent, PackDraft dependency)
        {
            HashSet<EntityId> visited = [];
            Stack<EntityId> pending = new(dependent.Manifest.Dependencies.Select(item => item.PackId));
            while (pending.TryPop(out EntityId candidate))
            {
                if (candidate == dependency.Manifest.PackId)
                {
                    return true;
                }

                if (visited.Add(candidate) && validDrafts.TryGetValue(candidate, out PackDraft? draft))
                {
                    foreach (ContentDependency transitive in draft.Manifest.Dependencies)
                    {
                        pending.Push(transitive.PackId);
                    }
                }
            }

            return false;
        }
    }

    private static IReadOnlyList<PackDraft> ResolveLoadOrder(
        IReadOnlyList<PackDraft> drafts,
        HashSet<EntityId> invalid,
        ContentValidationReport report)
    {
        Dictionary<EntityId, PackDraft> remaining = drafts
            .Where(draft => !invalid.Contains(draft.Manifest.PackId))
            .ToDictionary(draft => draft.Manifest.PackId);
        List<PackDraft> ordered = [];
        while (remaining.Count > 0)
        {
            bool builtInsRemain = remaining.Values.Any(draft => draft.Manifest.IsBuiltIn);
            PackDraft[] ready = remaining.Values
                .Where(draft => !builtInsRemain || draft.Manifest.IsBuiltIn)
                .Where(draft => draft.Manifest.Dependencies
                    .Where(dependency => remaining.ContainsKey(dependency.PackId))
                    .All(dependency => ordered.Any(pack => pack.Manifest.PackId == dependency.PackId)))
                .OrderBy(draft => draft.Manifest.IsBuiltIn ? 0 : 1)
                .ThenBy(draft => draft.Manifest.LoadPriority)
                .ThenBy(draft => draft.Manifest.PackId)
                .ToArray();
            if (ready.Length == 0)
            {
                foreach (PackDraft draft in remaining.Values.OrderBy(item => item.Manifest.PackId))
                {
                    report.Error("dependency.cycle", draft.ManifestPath, "$.dependencies", draft.Manifest.PackId, "Dependency cycle prevents deterministic loading.", "Remove one edge from the cycle.");
                    invalid.Add(draft.Manifest.PackId);
                }

                break;
            }

            foreach (PackDraft draft in ready)
            {
                ordered.Add(draft);
                remaining.Remove(draft.Manifest.PackId);
            }
        }

        return ordered;
    }

    private static void ValidateDraftSemantics(
        PackDraft draft,
        IReadOnlySet<EntityId> allRecordIds,
        IReadOnlySet<EntityId> allSourceIds,
        IReadOnlySet<string> allLocalization,
        IReadOnlySet<EntityId> allProvenanceIds,
        ContentValidationReport report,
        HashSet<EntityId> invalid)
    {
        int before = report.ErrorCount;
        ValidateUniqueIds(draft, report);
        foreach (ContentRecord record in draft.Records)
        {
            if (record.SchemaVersion != ContentContractVersions.Record
                || !record.Id.IsValid
                || string.IsNullOrWhiteSpace(record.RecordType)
                || record.SourceIds is null
                || record.LocalizationKeys is null
                || record.Data is null
                || !Enum.IsDefined(record.ContentTag)
                || !Enum.IsDefined(record.Classification)
                || record.SourceIds.Any(id => !id.IsValid)
                || record.LocalizationKeys.Any(id => !id.IsValid)
                || record.SourceIds.Distinct().Count() != record.SourceIds.Count
                || record.LocalizationKeys.Distinct().Count() != record.LocalizationKeys.Count)
            {
                report.Error("record.contract", draft.ManifestPath, "$.records", record.Id.IsValid ? record.Id : null, "Record envelope is invalid.", "Use the supported schema, stable ID, type, and unique reference lists.");
                continue;
            }

            if (record.ContentTag != ContentTag.Fictional && record.SourceIds.Count == 0)
            {
                report.Error("record.sources", draft.ManifestPath, "$.records", record.Id, $"{record.ContentTag} content has no source references.", "Link at least one SourceReference ID.");
            }

            if (record.ContentTag == ContentTag.Disputed && record.SourceIds.Count < 2)
            {
                report.Error("record.disputed_sources", draft.ManifestPath, "$.records", record.Id, "Disputed content requires at least two source records.", "Record each conflicting source rather than collapsing the dispute.");
            }

            foreach (EntityId sourceId in record.SourceIds)
            {
                if (!allSourceIds.Contains(sourceId))
                {
                    report.Error("source.missing", draft.ManifestPath, "$.records", record.Id, $"Source '{sourceId}' is missing.", "Add the referenced SourceReference record.");
                }
            }

            foreach (EntityId key in record.LocalizationKeys)
            {
                foreach (string locale in new[] { "en-US", "ko-KR" })
                {
                    if (allLocalization.Contains($"{key.Value}\n{locale}"))
                    {
                        continue;
                    }

                    if (record.ReleaseMarked)
                    {
                        report.Error("localization.coverage", draft.ManifestPath, "$.records", record.Id, $"Release string '{key}' is missing {locale}.", "Add the missing launch-language row.");
                    }
                    else
                    {
                        report.Warning("localization.coverage", draft.ManifestPath, "$.records", record.Id, $"Development string '{key}' is missing {locale}.", "Translate before marking the record for release.");
                    }
                }
            }

            if (record.ReleaseMarked && record.Classification == ContentClassification.SexuallyExplicit)
            {
                report.Error("release.explicit_content", draft.ManifestPath, "$.records", record.Id, "Sexually explicit content is prohibited before version 1.0.", "Remove it from the release manifest.");
            }

            if (record.Data["references"] is JsonArray references)
            {
                foreach (JsonNode? reference in references)
                {
                    if (reference is not JsonValue value
                        || !EntityId.TryParse(value.GetValue<string>(), out EntityId id)
                        || !allRecordIds.Contains(id))
                    {
                        report.Error("record.reference", draft.ManifestPath, "$.records[].data.references", record.Id, $"Record '{record.Id}' has a broken content reference.", "Reference an existing stable content ID.");
                    }
                }
            }

            ValidateTypedData(record, draft.ManifestPath, report);

            if (record.RecordType == "asset")
            {
                string? provenanceValue = record.Data["provenanceId"]?.GetValue<string>();
                if (!EntityId.TryParse(provenanceValue, out EntityId provenanceId)
                    || !allProvenanceIds.Contains(provenanceId))
                {
                    report.Error("provenance.missing", draft.ManifestPath, "$.records[].data.provenanceId", record.Id, "Asset record has no valid provenance reference.", "Reference a registered AssetProvenance ID.");
                }
            }
        }

        foreach (LocalizationEntry entry in draft.Localization)
        {
            if (entry.ReleaseMarked && entry.ReviewState != LocalizationReviewState.Approved)
            {
                report.Error("localization.review", draft.ManifestPath, "$.localization", entry.Key, "Release localization is not approved.", "Complete bilingual review and set review_state to approved.");
            }

            if (entry.SourceContentIds.Any(id => !allRecordIds.Contains(id)))
            {
                report.Error("localization.content_reference", draft.ManifestPath, "$.localization", entry.Key, "Localization references missing content.", "Reference an existing content ID.");
            }
        }

        foreach (IGrouping<EntityId, LocalizationEntry> messages in draft.Localization.GroupBy(entry => entry.Key))
        {
            string[] signatures = messages.Select(entry => Localization.BranchSignature(entry.Text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (signatures.Length > 1)
            {
                report.Error("localization.branches", draft.ManifestPath, "$.localization", messages.Key, "Plural/select branches differ between locales.", "Use the same selector branches in Korean and English messages.");
            }
        }

        foreach (SourceReference source in draft.Sources)
        {
            if (source.SchemaVersion != ContentContractVersions.SourceReference
                || !source.SourceId.IsValid
                || string.IsNullOrWhiteSpace(source.Claim)
                || string.IsNullOrWhiteSpace(source.Location)
                || string.IsNullOrWhiteSpace(source.Citation)
                || string.IsNullOrWhiteSpace(source.SourceTier)
                || source.Confidence is not ("high" or "medium" or "low"))
            {
                report.Error("source.contract", draft.ManifestPath, "$.sources", source.SourceId, "Source reference is incomplete.", "Record claim, location, citation, confidence, tier, and uncertainty notes.");
            }
        }

        foreach (AssetProvenance asset in draft.Assets)
        {
            if (!asset.AssetId.IsValid
                || asset.ContentIds is null
                || asset.ContentIds.Any(id => !id.IsValid)
                || asset.ContentIds.Distinct().Count() != asset.ContentIds.Count)
            {
                report.Error("provenance.contract", draft.ManifestPath, "$.assets", asset.AssetId.IsValid ? asset.AssetId : null, "Asset provenance identity or content links are invalid.", "Use stable unique IDs and the supported provenance schema.");
            }
            else
            {
                ValidateAssetProvenance(asset, draft.Manifest.ReleaseEligible, draft.ManifestPath, allRecordIds, report);
            }
        }

        if (draft.Glossary.Select(entry => entry.TermId).Distinct().Count() != draft.Glossary.Count)
        {
            report.Error("glossary.duplicate", draft.ManifestPath, "$.glossary", null, "Glossary contains duplicate stable term IDs.", "Keep one bilingual row per term ID.");
        }

        if (report.ErrorCount != before)
        {
            invalid.Add(draft.Manifest.PackId);
        }
    }

    private static void ValidateUniqueIds(PackDraft draft, ContentValidationReport report)
    {
        foreach (IGrouping<EntityId, ContentRecord> duplicate in draft.Records.GroupBy(record => record.Id).Where(group => group.Count() > 1))
        {
            report.Error("record.duplicate", draft.ManifestPath, "$.records", duplicate.Key, "Record ID is duplicated within one pack.", "Keep exactly one authored record for the ID.");
        }

        foreach (IGrouping<string, LocalizationEntry> duplicate in draft.Localization.GroupBy(LocalizationKey).Where(group => group.Count() > 1))
        {
            report.Error("localization.duplicate", draft.ManifestPath, "$.localization", duplicate.First().Key, "Localization key/locale pair is duplicated within one pack.", "Keep one translation per key and locale.");
        }
    }

    private static void ApplyPack(
        PackDraft draft,
        IDictionary<EntityId, ContentRecord> records,
        IDictionary<string, LocalizationEntry> localization,
        IDictionary<EntityId, GlossaryEntry> glossary,
        IDictionary<EntityId, SourceReference> sources,
        IDictionary<EntityId, AssetProvenance> assets,
        ContentValidationReport report)
    {
        foreach (ContentRecord record in draft.Records.OrderBy(item => item.Id))
        {
            if (!records.TryAdd(record.Id, record))
            {
                report.Error("record.implicit_override", draft.ManifestPath, "$.records", record.Id, "Duplicate records cannot implicitly replace earlier packs.", "Move changed fields into an explicit override document.");
            }
        }

        foreach (ContentOverride contentOverride in draft.Overrides.OrderBy(item => item.TargetId))
        {
            if (!records.TryGetValue(contentOverride.TargetId, out ContentRecord? target))
            {
                report.Error("override.target", draft.ManifestPath, "$.overrides", contentOverride.TargetId, "Override target does not exist in an earlier pack.", "Add the dependency and use the target's stable ID.");
                continue;
            }

            try
            {
                records[contentOverride.TargetId] = ApplyOverride(target, contentOverride);
            }
            catch (InvalidDataException exception)
            {
                report.Error("override.field", draft.ManifestPath, "$.overrides", contentOverride.TargetId, exception.Message, "Use a unique JSON pointer to an allowed mutable field.");
            }
        }

        foreach (LocalizationEntry entry in draft.Localization.OrderBy(item => item.Key).ThenBy(item => item.Locale, StringComparer.Ordinal))
        {
            string key = LocalizationKey(entry);
            if (!localization.TryAdd(key, entry))
            {
                report.Error("localization.conflict", draft.ManifestPath, "$.localization", entry.Key, "Localization entry conflicts with an earlier pack.", "Use an explicit content override rather than duplicate localization rows.");
            }
        }

        foreach (GlossaryEntry entry in draft.Glossary.OrderBy(item => item.TermId))
        {
            if (!glossary.TryAdd(entry.TermId, entry))
            {
                report.Error("glossary.conflict", draft.ManifestPath, "$.glossary", entry.TermId, "Glossary term conflicts with an earlier pack.", "Keep one authoritative bilingual glossary entry per stable term ID.");
            }
        }

        foreach (SourceReference source in draft.Sources)
        {
            if (!sources.TryAdd(source.SourceId, source))
            {
                report.Error("source.duplicate", draft.ManifestPath, "$.sources", source.SourceId, "Source reference ID is duplicated.", "Use one stable source record.");
            }
        }

        foreach (AssetProvenance asset in draft.Assets)
        {
            if (!assets.TryAdd(asset.AssetId, asset))
            {
                report.Error("provenance.duplicate", draft.ManifestPath, "$.assets", asset.AssetId, "Asset provenance ID is duplicated.", "Use one provenance record per final asset.");
            }
        }

        foreach (EntityId targetId in draft.Overrides.Select(item => item.TargetId).Distinct())
        {
            if (records.TryGetValue(targetId, out ContentRecord? updated))
            {
                ValidateAppliedOverride(updated, draft.ManifestPath, records, localization, sources, assets, report);
            }
        }
    }

    private static void ValidateAppliedOverride(
        ContentRecord record,
        string file,
        IDictionary<EntityId, ContentRecord> records,
        IDictionary<string, LocalizationEntry> localization,
        IDictionary<EntityId, SourceReference> sources,
        IDictionary<EntityId, AssetProvenance> assets,
        ContentValidationReport report)
    {
        ValidateTypedData(record, file, report);
        if (record.ContentTag != ContentTag.Fictional && record.SourceIds.Count == 0)
        {
            report.Error("record.sources", file, "$.overrides", record.Id, "Override removed required historical source evidence.", "Restore source IDs or use an appropriate fictional tag.");
        }

        if (record.ContentTag == ContentTag.Disputed && record.SourceIds.Count < 2)
        {
            report.Error("record.disputed_sources", file, "$.overrides", record.Id, "Override leaves disputed content with fewer than two sources.", "Reference both conflicting source records.");
        }

        if (record.SourceIds.Any(id => !sources.ContainsKey(id)))
        {
            report.Error("source.missing", file, "$.overrides", record.Id, "Override references a missing source.", "Add the source record through a declared dependency.");
        }

        if (record.Data["references"] is JsonArray references
            && references.Any(reference => reference is not JsonValue value
                || !EntityId.TryParse(value.GetValue<string>(), out EntityId id)
                || !records.ContainsKey(id)))
        {
            report.Error("record.reference", file, "$.overrides", record.Id, "Override creates a broken content reference.", "Reference an existing stable content ID.");
        }

        if (record.ReleaseMarked)
        {
            if (record.Classification == ContentClassification.SexuallyExplicit)
            {
                report.Error("release.explicit_content", file, "$.overrides", record.Id, "Override makes explicit content release-eligible.", "Remove the explicit classification from pre-1.0 release content.");
            }

            foreach (EntityId key in record.LocalizationKeys)
            {
                foreach (string locale in new[] { "en-US", "ko-KR" })
                {
                    if (!localization.TryGetValue($"{key.Value}\n{locale}", out LocalizationEntry? entry)
                        || entry.ReviewState != LocalizationReviewState.Approved)
                    {
                        report.Error("localization.coverage", file, "$.overrides", record.Id, $"Override release string '{key}' lacks approved {locale} text.", "Add and approve both launch-language entries.");
                    }
                }
            }
        }

        if (record.RecordType == "asset")
        {
            string? value = record.Data["provenanceId"]?.GetValue<string>();
            if (!EntityId.TryParse(value, out EntityId provenanceId) || !assets.ContainsKey(provenanceId))
            {
                report.Error("provenance.missing", file, "$.overrides", record.Id, "Override creates an asset without provenance.", "Reference a loaded provenance record.");
            }
        }
    }

    private static ContentRecord ApplyOverride(ContentRecord target, ContentOverride contentOverride)
    {
        if (contentOverride.SchemaVersion != ContentContractVersions.Record
            || contentOverride.Fields.Count == 0
            || contentOverride.Fields.Select(field => field.JsonPath).Distinct(StringComparer.Ordinal).Count() != contentOverride.Fields.Count)
        {
            throw new InvalidDataException("Override contract is invalid or contains duplicate field paths.");
        }

        JsonObject root = JsonSerializer.SerializeToNode(target, JsonOptions)!.AsObject();
        foreach (FieldOverride field in contentOverride.Fields.OrderBy(item => item.JsonPath, StringComparer.Ordinal))
        {
            if (field.JsonPath is "/id" or "/schemaVersion" or "/recordType"
                || (!field.JsonPath.StartsWith("/data/", StringComparison.Ordinal)
                    && field.JsonPath is not ("/contentTag" or "/classification" or "/sourceIds" or "/localizationKeys" or "/releaseMarked")))
            {
                throw new InvalidDataException($"Override path '{field.JsonPath}' is not allowed.");
            }

            SetJsonPointer(root, field.JsonPath, field.Value?.DeepClone());
        }

        return root.Deserialize<ContentRecord>(JsonOptions)
            ?? throw new InvalidDataException("Override produced an empty record.");
    }

    private static void SetJsonPointer(JsonObject root, string pointer, JsonNode? value)
    {
        string[] parts = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();
        JsonNode current = root;
        for (int index = 0; index < parts.Length - 1; index++)
        {
            if (current is not JsonObject currentObject || currentObject[parts[index]] is not JsonNode next)
            {
                throw new InvalidDataException($"Override path '{pointer}' does not exist.");
            }

            current = next;
        }

        if (current is not JsonObject target || !target.ContainsKey(parts[^1]))
        {
            throw new InvalidDataException($"Override path '{pointer}' does not exist.");
        }

        target[parts[^1]] = value;
    }

    private static void ValidateRegistry(
        IReadOnlyDictionary<EntityId, ContentRecord> records,
        IReadOnlyDictionary<string, LocalizationEntry> localization,
        IReadOnlyDictionary<EntityId, SourceReference> sources,
        IReadOnlyDictionary<EntityId, AssetProvenance> assets,
        IReadOnlyList<LoadedContentPack> packs,
        ContentValidationReport report)
    {
        foreach (ContentRecord record in records.Values.OrderBy(item => item.Id))
        {
            foreach (EntityId sourceId in record.SourceIds)
            {
                if (!sources.ContainsKey(sourceId))
                {
                    report.Error("source.missing", "registry", "$.records", record.Id, $"Source '{sourceId}' is missing.", "Add the referenced SourceReference record.");
                }
            }

            foreach (EntityId key in record.LocalizationKeys)
            {
                foreach (string locale in new[] { "en-US", "ko-KR" })
                {
                    if (localization.ContainsKey($"{key.Value}\n{locale}"))
                    {
                        continue;
                    }

                    if (record.ReleaseMarked)
                    {
                        report.Error("localization.coverage", "registry", "$.records", record.Id, $"Release string '{key}' is missing {locale}.", "Add the missing launch-language row.");
                    }
                    else
                    {
                        report.Warning("localization.coverage", "registry", "$.records", record.Id, $"Development string '{key}' is missing {locale}.", "Translate before marking the record for release.");
                    }
                }
            }
        }

        foreach (LocalizationEntry entry in localization.Values)
        {
            foreach (EntityId contentId in entry.SourceContentIds)
            {
                if (!records.ContainsKey(contentId))
                {
                    report.Error("localization.content_reference", "registry", "$.localization", entry.Key, $"Localization source content '{contentId}' is missing.", "Reference an existing content record.");
                }
            }
        }

        foreach (SourceReference source in sources.Values)
        {
            if (source.SchemaVersion != ContentContractVersions.SourceReference
                || string.IsNullOrWhiteSpace(source.Claim)
                || string.IsNullOrWhiteSpace(source.Location)
                || string.IsNullOrWhiteSpace(source.Citation)
                || source.Confidence is not ("high" or "medium" or "low"))
            {
                report.Error("source.contract", "registry", "$.sources", source.SourceId, "Source reference is incomplete.", "Record claim, location, citation, confidence, tier, and uncertainty notes.");
            }
        }

        bool releasePack = packs.Any(pack => pack.Manifest.ReleaseEligible);
        foreach (AssetProvenance asset in assets.Values)
        {
            ValidateAssetProvenance(asset, releasePack, "registry", records.Keys.ToHashSet(), report);
        }
    }

    private static void ValidateAssetProvenance(
        AssetProvenance asset,
        bool releasePack,
        string file,
        IReadOnlySet<EntityId> recordIds,
        ContentValidationReport report)
    {
        bool effectiveRelease = asset.ReleaseEligible || releasePack;
        bool completeBase = asset.SchemaVersion == ContentContractVersions.AssetProvenance
            && asset.ContentIds.Count > 0
            && asset.HumanApproved
            && !string.IsNullOrWhiteSpace(asset.RightsStatus)
            && !string.IsNullOrWhiteSpace(asset.HumanEdits)
            && !string.IsNullOrWhiteSpace(asset.Reviewer)
            && !string.IsNullOrWhiteSpace(asset.CommercialRightsEvidence)
            && !string.IsNullOrWhiteSpace(asset.Sha256)
            && asset.Sha256.Length == 64
            && asset.Sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        bool completeAi = asset.Origin != AssetOrigin.OfflineAi
            || (!string.IsNullOrWhiteSpace(asset.ModelServiceVersion)
                && !string.IsNullOrWhiteSpace(asset.GenerationDate)
                && DateOnly.TryParseExact(
                    asset.GenerationDate,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out _)
                && !string.IsNullOrWhiteSpace(asset.InputSources)
                && !string.IsNullOrWhiteSpace(asset.PromptBrief)
                && completeBase);
        if (releasePack && !asset.ReleaseEligible)
        {
            report.Error("provenance.development_only", file, "$.assets", asset.AssetId, "Development-only asset is included in a release pack.", "Approve the asset or remove it from the release manifest.");
        }

        if (effectiveRelease && (!completeBase || !completeAi))
        {
            report.Error("provenance.incomplete", file, "$.assets", asset.AssetId, "Release asset provenance or human approval is incomplete.", "Complete every AI/right/reviewer/checksum field before release.");
        }

        if (effectiveRelease && asset.Origin == AssetOrigin.LiveGenerated)
        {
            report.Error("release.live_generation", file, "$.assets", asset.AssetId, "Live-generated assets are prohibited.", "Use reviewed offline production with full provenance.");
        }

        if (effectiveRelease && asset.Classification == ContentClassification.SexuallyExplicit)
        {
            report.Error("release.explicit_asset", file, "$.assets", asset.AssetId, "Sexually explicit assets are prohibited before version 1.0.", "Remove the asset from the release pack.");
        }

        if (releasePack && asset.ContentIds.Any(id => !recordIds.Contains(id)))
        {
            report.Error("provenance.content_reference", file, "$.assets", asset.AssetId, "Asset provenance references missing content.", "Reference existing content IDs.");
        }
    }

    private static void ValidateTypedData(
        ContentRecord record,
        string file,
        ContentValidationReport report)
    {
        switch (record.RecordType)
        {
            case "region":
                if (!TryGetInt64(record.Data["population"], out long population)
                    || population is < 0 or > 1_000_000_000)
                {
                    report.Error("record.range", file, "$.records[].data.population", record.Id, "Region population must be an integer from 0 through 1,000,000,000.", "Correct the authored population value.");
                }

                break;
            case "character":
                if (!TryGetInt64(record.Data["birthYear"], out long birthYear)
                    || birthYear is < 1 or > 9999)
                {
                    report.Error("record.date", file, "$.records[].data.birthYear", record.Id, "Character birthYear is outside the proleptic calendar.", "Use a year from 1 through 9999.");
                }

                if (record.Data["deathYear"] is not null
                    && (!TryGetInt64(record.Data["deathYear"], out long deathYear)
                        || deathYear < birthYear
                        || deathYear > 9999))
                {
                    report.Error("record.date", file, "$.records[].data.deathYear", record.Id, "Character deathYear precedes birthYear or is invalid.", "Use a valid year on or after birthYear.");
                }

                break;
            case "dated_event":
                if (record.Data["date"] is not JsonValue dateValue
                    || !TryParseDate(dateValue.GetValue<string>()))
                {
                    report.Error("record.date", file, "$.records[].data.date", record.Id, "Event date is not a valid proleptic YYYY-MM-DD date.", "Use an invariant project calendar date.");
                }

                break;
        }
    }

    private static bool TryGetInt64(JsonNode? node, out long value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseDate(string? value)
    {
        string[] parts = value?.Split('-') ?? [];
        if (parts.Length != 3
            || !int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out int year)
            || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out int month)
            || !int.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out int day))
        {
            return false;
        }

        try
        {
            _ = new CampaignDate(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string LocalizationKey(LocalizationEntry entry) => $"{entry.Key.Value}\n{entry.Locale}";

    private static string DisplayPath(string path, string? root) => root is null
        ? path.Replace('\\', '/')
        : Path.GetRelativePath(root, path).Replace('\\', '/');

    private static bool IsPortableContentPath(string path)
    {
        if (path.Length is 0 or > 240 || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = path.Split('/');
        return segments.All(segment => segment.Length > 0
            && segment is not ("." or "..")
            && segment.All(character => character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '_' or '-'));
    }

    private sealed record PackDraft(
        ContentManifest Manifest,
        string ManifestPath,
        string Checksum,
        IReadOnlyList<ContentRecord> Records,
        IReadOnlyList<ContentOverride> Overrides,
        IReadOnlyList<LocalizationEntry> Localization,
        IReadOnlyList<GlossaryEntry> Glossary,
        IReadOnlyList<SourceReference> Sources,
        IReadOnlyList<AssetProvenance> Assets);
}
