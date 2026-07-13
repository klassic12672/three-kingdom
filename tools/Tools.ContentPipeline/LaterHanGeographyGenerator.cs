using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Game.Content;
using Simulation.Core;

namespace Tools.ContentPipeline;

public static partial class LaterHanGeographyGenerator
{
    private const string PrimaryTextSource = "source:hou_han_shu_jun_guo";
    private const string AuditedWorkbookSource = "source:later_han_audited_workbook";
    private const string UnicodeSource = "source:unicode_unihan_17";
    private const string DilaLocationSource = "source:dila_place_authority_2026_07";
    private const string LayoutSource = "source:later_han_stylized_layout";

    private static readonly JsonSerializerOptions JsonOptions = ContentJson.CreateOptions(indented: true);
    private static readonly JsonSerializerOptions SourceJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, TerrainType> ProvinceTerrains =
        new Dictionary<string, TerrainType>(StringComparer.Ordinal)
        {
            ["A1"] = TerrainType.Plains,
            ["H1"] = TerrainType.Plains,
            ["N1"] = TerrainType.Plains,
            ["W1"] = TerrainType.Plains,
            ["AE1"] = TerrainType.Marsh,
            ["AJ1"] = TerrainType.Coast,
            ["AP1"] = TerrainType.Hills,
            ["AW1"] = TerrainType.Hills,
            ["BC1"] = TerrainType.Mountains,
            ["BO1"] = TerrainType.Mountains,
            ["CA1"] = TerrainType.Hills,
            ["CJ1"] = TerrainType.Hills,
            ["CU1"] = TerrainType.Forest,
        };

    private static readonly IReadOnlyDictionary<string, StopPlacement> StopPlacements =
        new Dictionary<string, StopPlacement>(StringComparer.Ordinal)
        {
            ["stop:year191/luoyang_city"] = new("A4", 0, 0, 30),
            ["stop:year191/mengjin_port"] = new("A4", 12, -38, 8),
            ["stop:year191/yellow_river_ferry"] = new("B5", 10, -10, 5),
            ["stop:year191/hulao_gate"] = new("A7", -4, 2, 90),
            ["stop:year191/xingyang_fort"] = new("A7", 25, 10, 55),
            ["stop:year191/yingyin_city"] = new("H13", 0, 0, 18),
            ["stop:year191/chenliu_depot"] = new("W4", 0, 0, 12),
            ["stop:year191/songshan_pass"] = new("H18", 0, 0, 240),
        };

    public static GenerationResult Generate(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string sourcePath = Path.Combine(root, "data", "research", "later-han-administrative-units.json");
        string locationPath = Path.Combine(root, "data", "research", "later-han-locations.json");
        string recordsPath = Path.Combine(root, "data", "authored", "later-han-geography.json");
        string englishPath = Path.Combine(root, "data", "localization", "later-han.en-US.csv");
        string koreanPath = Path.Combine(root, "data", "localization", "later-han.ko-KR.csv");
        string scenarioPath = Path.Combine(root, "data", "authored", "geography-191.json");
        string manifestPath = Path.Combine(root, "data", "content-manifest.json");

        LaterHanSourceDocument source = JsonSerializer.Deserialize<LaterHanSourceDocument>(
            File.ReadAllText(sourcePath),
            SourceJsonOptions) ?? throw new InvalidDataException("Later Han source snapshot is empty.");
        ValidateSource(source);
        LaterHanLocationDocument locations = JsonSerializer.Deserialize<LaterHanLocationDocument>(
            File.ReadAllText(locationPath), SourceJsonOptions)
            ?? throw new InvalidDataException("Later Han location snapshot is empty.");
        ValidateLocations(source, locations);

        Dictionary<string, SourceRecord> byCell = source.Records.ToDictionary(item => item.SourceCell, StringComparer.Ordinal);
        Dictionary<string, MapPoint> anchors = BuildAnchors(source.Records, locations.Records);
        ContentRecord[] records = source.Records
            .OrderBy(item => LevelOrder(item.Level))
            .ThenBy(item => CellOrder(item.SourceCell))
            .Select(item => CreateContentRecord(item, byCell, anchors[item.SourceCell]))
            .ToArray();

        WriteJson(recordsPath, new ContentRecordDocument(1, records));
        WriteLocalization(englishPath, source.Records, locale: "en-US");
        WriteLocalization(koreanPath, source.Records, locale: "ko-KR");
        PatchScenario(scenarioPath, source.Records, anchors);
        UpdateManifest(manifestPath, root);

        return new GenerationResult(
            records.Count(item => item.RecordType == "region"),
            records.Count(item => item.RecordType == "district"),
            records.Count(item => item.RecordType == "locality"),
            recordsPath,
            englishPath,
            koreanPath);
    }

    private static void ValidateLocations(LaterHanSourceDocument source, LaterHanLocationDocument locations)
    {
        if (locations.SchemaVersion != 1 || locations.Records.Count != source.Records.Count
            || locations.Records.Select(item => item.SourceCell).Distinct(StringComparer.Ordinal).Count() != locations.Records.Count
            || source.Records.Any(row => locations.Records.All(item => item.SourceCell != row.SourceCell))
            || locations.Records.Any(item => item.Longitude is < 90 or > 128 || item.Latitude is < 15 or > 46
                || item.PlacementStatus is not ("dila_direct" or "dila_child_disambiguated" or "dila_parent_disambiguated"
                    or "child_centroid_inferred" or "descendant_centroid_inferred" or "parent_inferred")))
        {
            throw new InvalidDataException("Later Han location snapshot must contain one valid attributed or inferred anchor for every hierarchy row.");
        }
    }

    private static void ValidateSource(LaterHanSourceDocument source)
    {
        if (source.SchemaVersion != 1 || source.Records is null
            || source.Records.Count(item => item.Level == "province") != 13
            || source.Records.Count(item => item.Level == "district") != 99
            || source.Records.Count(item => item.Level == "locality") != 1160)
        {
            throw new InvalidDataException("Later Han source snapshot must contain schema 1 with 13 regions, 99 districts, and 1,160 localities.");
        }

        if (source.Records.Any(item => string.IsNullOrWhiteSpace(item.SourceCell)
                || string.IsNullOrWhiteSpace(item.Hanja)
                || string.IsNullOrWhiteSpace(item.Korean)
                || string.IsNullOrWhiteSpace(item.English)
                || item.Level is not ("province" or "district" or "locality"))
            || source.Records.Select(item => item.SourceCell).Distinct(StringComparer.Ordinal).Count() != source.Records.Count)
        {
            throw new InvalidDataException("Later Han source snapshot contains an invalid or duplicate row.");
        }

        HashSet<string> cells = source.Records.Select(item => item.SourceCell).ToHashSet(StringComparer.Ordinal);
        if (source.Records.Any(item => item.Level != "province"
                && (item.ParentSourceCell is null || !cells.Contains(item.ParentSourceCell))))
        {
            throw new InvalidDataException("Later Han source snapshot contains a missing parent source cell.");
        }

        if (ProvinceTerrains.Keys.Any(cell => !cells.Contains(cell)))
        {
            throw new InvalidDataException("Stylized layout is missing a required province source row.");
        }
    }

    private static Dictionary<string, MapPoint> BuildAnchors(
        IReadOnlyList<SourceRecord> records,
        IReadOnlyList<LocationRecord> locations)
    {
        Dictionary<string, SourceRecord> byCell = records.ToDictionary(item => item.SourceCell, StringComparer.Ordinal);
        Dictionary<string, MapPoint> anchors = new(StringComparer.Ordinal);
        foreach (LocationRecord location in locations)
        {
            SourceRecord row = byCell[location.SourceCell];
            TerrainType terrain = TerrainFor(ProvinceTerrain(row, byCell), row.SourceCell);
            int x = (int)Math.Round(90 + (location.Longitude - 92d) / 34d * 1_220d);
            int y = (int)Math.Round(65 + (44.5d - location.Latitude) / 27.5d * 950d);
            anchors[row.SourceCell] = new(Math.Clamp(x, 65, 1_335), Math.Clamp(y, 45, 1_035), Elevation(terrain));
        }

        return anchors;
    }

    private static ContentRecord CreateContentRecord(
        SourceRecord source,
        IReadOnlyDictionary<string, SourceRecord> byCell,
        MapPoint anchor)
    {
        string recordType = source.Level switch
        {
            "province" => "region",
            "district" => "district",
            "locality" => "locality",
            _ => throw new InvalidDataException($"Unsupported Later Han level '{source.Level}'."),
        };
        EntityId id = new(IdFor(source));
        EntityId nameKey = new($"loc:later_han/{recordType}/{CellToken(source.SourceCell)}/name");
        EntityId labelKey = new(LabelKey(source.UnitType));
        EntityId[] sourceIds =
        [
            new(PrimaryTextSource),
            new(AuditedWorkbookSource),
            new(UnicodeSource),
            new(DilaLocationSource),
            new(LayoutSource),
        ];
        JsonObject data = new()
        {
            ["nameKey"] = nameKey.Value,
            ["labelKey"] = labelKey.Value,
            ["population"] = 0,
            ["cultureId"] = "culture:han",
            ["anchor"] = JsonSerializer.SerializeToNode(anchor, JsonOptions),
        };
        List<EntityId> references = [];
        if (source.Level == "district")
        {
            SourceRecord parent = byCell[source.ParentSourceCell!];
            EntityId regionId = new(IdFor(parent));
            data["regionId"] = regionId.Value;
            references.Add(regionId);
        }
        else if (source.Level == "locality")
        {
            SourceRecord parent = byCell[source.ParentSourceCell!];
            EntityId districtId = new(IdFor(parent));
            data["districtId"] = districtId.Value;
            data["terrain"] = TerrainFor(ProvinceTerrain(source, byCell), source.SourceCell).ToString().ToLowerInvariant();
            references.Add(districtId);
        }

        data["references"] = new JsonArray(references.Select(reference => JsonValue.Create(reference.Value)).ToArray());
        return new ContentRecord(
            1,
            id,
            recordType,
            ContentTag.Historical,
            ContentClassification.General,
            sourceIds,
            [nameKey, labelKey],
            source.KoreanStatus != "provisional",
            data);
    }

    private static TerrainType ProvinceTerrain(SourceRecord source, IReadOnlyDictionary<string, SourceRecord> byCell)
    {
        SourceRecord current = source;
        while (current.Level != "province")
        {
            current = byCell[current.ParentSourceCell!];
        }

        return ProvinceTerrains[current.SourceCell];
    }

    private static void WriteLocalization(string path, IReadOnlyList<SourceRecord> records, string locale)
    {
        StringBuilder csv = new("key,locale,text,context,variables,review_state,source_content_ids,release_marked\n");
        foreach ((string key, string english, string korean) in AdministrativeLabels())
        {
            AppendCsv(csv, [key, locale, locale == "ko-KR" ? korean : english, "Later Han administrative label", "", "approved", "", "true"]);
        }

        foreach (SourceRecord record in records.OrderBy(item => LevelOrder(item.Level)).ThenBy(item => CellOrder(item.SourceCell)))
        {
            string recordType = record.Level switch { "province" => "region", "district" => "district", _ => "locality" };
            string key = $"loc:later_han/{recordType}/{CellToken(record.SourceCell)}/name";
            string display = locale == "ko-KR" ? record.Korean : record.English;
            string review = locale == "ko-KR" && record.KoreanStatus == "provisional" ? "reviewed" : "approved";
            string releaseMarked = record.KoreanStatus == "provisional" ? "false" : "true";
            string context = $"Later Han {record.Level} name; {record.Hanja}; audited source cell {record.SourceCell}";
            AppendCsv(csv, [key, locale, display, context, "", review, IdFor(record), releaseMarked]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IEnumerable<(string Key, string English, string Korean)> AdministrativeLabels()
    {
        yield return ("loc:admin/capital_district", "Capital District", "경기");
        yield return ("loc:admin/guo", "Kingdom", "국");
        yield return ("loc:admin/houguo", "Marquisate", "후국");
        yield return ("loc:admin/yi", "Estate", "읍");
        yield return ("loc:admin/gongguo", "Duchy", "공국");
        yield return ("loc:admin/dao", "Dao", "도");
        yield return ("loc:admin/locality", "Locality", "지역");
    }

    private static void PatchScenario(string path, IReadOnlyList<SourceRecord> source, IReadOnlyDictionary<string, MapPoint> anchors)
    {
        JsonObject document = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException("geography-191.json is empty.");
        JsonArray records = document["records"]?.AsArray()
            ?? throw new InvalidDataException("geography-191.json has no records array.");
        for (int index = records.Count - 1; index >= 0; index--)
        {
            string? recordType = records[index]?["recordType"]?.GetValue<string>();
            if (recordType is "region" or "district" or "locality")
            {
                records.RemoveAt(index);
            }
        }

        Dictionary<string, SourceRecord> byCell = source.ToDictionary(item => item.SourceCell, StringComparer.Ordinal);
        foreach (JsonNode? node in records)
        {
            if (node is not JsonObject record || record["recordType"]?.GetValue<string>() != "route_stop")
            {
                continue;
            }

            string id = record["id"]!.GetValue<string>();
            if (!StopPlacements.TryGetValue(id, out StopPlacement? placement))
            {
                continue;
            }

            JsonObject data = record["data"]!.AsObject();
            SourceRecord locality = byCell[placement.LocalitySourceCell];
            MapPoint anchor = anchors[placement.LocalitySourceCell];
            string localityId = IdFor(locality);
            data["localityId"] = localityId;
            data["position"] = JsonSerializer.SerializeToNode(
                new MapPoint(anchor.X + placement.OffsetX, anchor.Y + placement.OffsetY, placement.Elevation),
                JsonOptions);
            data["references"] = new JsonArray(JsonValue.Create(localityId));
        }

        JsonObject scenario = records
            .Select(node => node?.AsObject())
            .Single(record => record?["recordType"]?.GetValue<string>() == "geography_scenario")!;
        JsonObject scenarioData = scenario["data"]!.AsObject();
        string[] regionIds = source.Where(item => item.Level == "province").OrderBy(item => CellOrder(item.SourceCell)).Select(IdFor).ToArray();
        string[] districtIds = source.Where(item => item.Level == "district").OrderBy(item => CellOrder(item.SourceCell)).Select(IdFor).ToArray();
        string[] localityIds = source.Where(item => item.Level == "locality").OrderBy(item => CellOrder(item.SourceCell)).Select(IdFor).ToArray();
        string[] stopIds = scenarioData["stopIds"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        string[] routeIds = scenarioData["routeIds"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        scenarioData["regionIds"] = ToJsonArray(regionIds);
        scenarioData["districtIds"] = ToJsonArray(districtIds);
        scenarioData["localityIds"] = ToJsonArray(localityIds);
        scenarioData["references"] = ToJsonArray(regionIds.Concat(districtIds).Concat(localityIds).Concat(stopIds).Concat(routeIds));

        File.WriteAllText(path, document.ToJsonString(JsonOptions) + Environment.NewLine);
    }

    private static void UpdateManifest(string manifestPath, string root)
    {
        ContentManifest manifest = JsonSerializer.Deserialize<ContentManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Content manifest is empty.");
        Dictionary<string, ContentFile> files = manifest.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        files["authored/later-han-geography.json"] = new(
            "authored/later-han-geography.json", ContentFileKind.Records, string.Empty);
        files["localization/later-han.en-US.csv"] = new(
            "localization/later-han.en-US.csv", ContentFileKind.Localization, string.Empty);
        files["localization/later-han.ko-KR.csv"] = new(
            "localization/later-han.ko-KR.csv", ContentFileKind.Localization, string.Empty);
        ContentFile[] updatedFiles = files.Values
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => file with { Sha256 = ContentChecksum.ComputeFile(Path.Combine(root, "data", file.Path)) })
            .ToArray();
        ContentManifest updated = manifest with
        {
            Version = "0.2.0",
            Files = updatedFiles,
            Provenance = manifest.Provenance with
            {
                License = "Mixed: project-owned gameplay compilation; DILA-derived location snapshot CC BY-SA 3.0; Unicode readings Unicode-3.0",
                RightsHolder = "Three Kingdoms project and respective cited source licensors",
            },
        };
        updated = updated with { Checksum = ContentChecksum.ComputePack(updated) };
        WriteJson(manifestPath, updated);
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values) =>
        new(values.Select(value => JsonValue.Create(value)).ToArray());

    private static string IdFor(SourceRecord source) =>
        $"{(source.Level == "province" ? "region" : source.Level)}:later_han/{CellToken(source.SourceCell)}";

    private static string LabelKey(string unitType) => unitType switch
    {
        "province" => "loc:admin/zhou",
        "capital_district" => "loc:admin/capital_district",
        "commandery" => "loc:admin/jun",
        "kingdom" => "loc:admin/guo",
        "county" => "loc:admin/xian",
        "estate" => "loc:admin/yi",
        "marquisate" => "loc:admin/houguo",
        "duchy" => "loc:admin/gongguo",
        "local_state" => "loc:admin/guo",
        "dao_county" => "loc:admin/dao",
        "locality_other" => "loc:admin/locality",
        _ => throw new InvalidDataException($"Unsupported Later Han unit type '{unitType}'."),
    };

    private static string CellToken(string sourceCell)
    {
        Match match = CellPattern().Match(sourceCell);
        if (!match.Success)
        {
            throw new InvalidDataException($"Invalid source cell '{sourceCell}'.");
        }

        return $"{match.Groups["column"].Value.ToLowerInvariant()}{int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture):000}";
    }

    private static long CellOrder(string sourceCell)
    {
        Match match = CellPattern().Match(sourceCell);
        if (!match.Success)
        {
            return long.MaxValue;
        }

        long column = 0;
        foreach (char character in match.Groups["column"].Value)
        {
            column = checked(column * 26 + (character - 'A' + 1));
        }

        long row = long.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
        return checked(column * 10_000 + row);
    }

    private static int LevelOrder(string level) => level switch { "province" => 0, "district" => 1, _ => 2 };

    private static TerrainType TerrainFor(TerrainType baseTerrain, string sourceCell)
    {
        uint hash = StableHash(sourceCell);
        return baseTerrain switch
        {
            TerrainType.Mountains => hash % 4 == 0 ? TerrainType.Hills : TerrainType.Mountains,
            TerrainType.Hills => hash % 5 == 0 ? TerrainType.Forest : hash % 3 == 0 ? TerrainType.Plains : TerrainType.Hills,
            TerrainType.Forest => hash % 4 == 0 ? TerrainType.Hills : TerrainType.Forest,
            TerrainType.Marsh => hash % 3 == 0 ? TerrainType.Plains : TerrainType.Marsh,
            TerrainType.Coast => hash % 3 == 0 ? TerrainType.Marsh : TerrainType.Coast,
            _ => hash % 9 == 0 ? TerrainType.Hills : TerrainType.Plains,
        };
    }

    private static int Elevation(TerrainType terrain) => terrain switch
    {
        TerrainType.Mountains => 180,
        TerrainType.Hills => 80,
        TerrainType.Forest => 45,
        TerrainType.Coast or TerrainType.Marsh => 6,
        _ => 15,
    };

    private static int Jitter(string value, int amplitude, int salt) =>
        (int)(StableHash($"{salt}:{value}") % (uint)(amplitude * 2 + 1)) - amplitude;

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (byte item in Encoding.UTF8.GetBytes(value))
        {
            hash ^= item;
            hash *= 16777619;
        }

        return hash;
    }

    private static void AppendCsv(StringBuilder csv, IReadOnlyList<string> values) =>
        csv.AppendJoin(',', values.Select(EscapeCsv)).Append('\n');

    private static string EscapeCsv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
    }

    [GeneratedRegex("^(?<column>[A-Z]+)(?<row>[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CellPattern();

    private sealed record LaterHanSourceDocument(
        int SchemaVersion,
        string DatasetId,
        SourceCounts Counts,
        IReadOnlyList<SourceRecord> Records);

    private sealed record SourceCounts(int Province, int District, int Locality);

    private sealed record LaterHanLocationDocument(
        int SchemaVersion,
        string DatasetId,
        LocationCounts Counts,
        IReadOnlyList<LocationRecord> Records);

    private sealed record LocationCounts(int Total, IReadOnlyDictionary<string, int> ByPlacementStatus);

    private sealed record LocationRecord(
        string SourceCell,
        double Longitude,
        double Latitude,
        string PlacementStatus,
        string Confidence,
        IReadOnlyList<string> SourcePlaceIds,
        IReadOnlyList<string> CoordinateCertainties);

    private sealed record SourceRecord(
        string SourceCell,
        string Level,
        string UnitType,
        string? ParentSourceCell,
        string Hanja,
        string Korean,
        string KoreanStatus,
        string English,
        string EnglishStatus,
        string CorrectionStatus,
        string Confidence,
        string ScopeStatus,
        string PrimaryTextStatus,
        string? PrimaryTextUrl,
        string? CorrectionNote);

    private sealed record StopPlacement(
        string LocalitySourceCell,
        int OffsetX,
        int OffsetY,
        int Elevation);
}

public sealed record GenerationResult(
    int Regions,
    int Districts,
    int Localities,
    string RecordsPath,
    string EnglishLocalizationPath,
    string KoreanLocalizationPath);
