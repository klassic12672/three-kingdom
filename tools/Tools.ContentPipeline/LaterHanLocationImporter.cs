using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Tools.ContentPipeline;

/// <summary>Builds the portable, attributed map-anchor snapshot without shipping the upstream TEI archive.</summary>
public static class LaterHanLocationImporter
{
    public const string PinnedDilaCommit = "385e3f557285d7a60346f85d698193e19b6cea2f";
    public const string PinnedDilaSha256 = "6fcc9f650b0737f4379f58d605cb65de5ce08680de8ab5631dbc1427f3552efb";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] RankSuffixes = ["侯國", "公國", "屬國", "郡", "國", "尹", "縣", "邑", "道", "州"];
    private static readonly IReadOnlyDictionary<string, Bounds> RegionBounds = new Dictionary<string, Bounds>(StringComparer.Ordinal)
    {
        ["司隸"] = new(109, 115.5, 32.5, 36.7), ["豫州"] = new(110, 117.5, 30.5, 35.8),
        ["冀州"] = new(112, 119.5, 35, 40.8), ["兗州"] = new(113.5, 119.5, 32.5, 37.8),
        ["徐州"] = new(114.5, 122.5, 30.5, 36.5), ["青州"] = new(116, 123, 34, 39),
        ["荊州"] = new(107, 116.5, 24, 34.5), ["揚州"] = new(114, 123, 23, 34),
        ["益州"] = new(96, 112, 20, 35), ["涼州"] = new(92, 109, 31, 43),
        ["並州"] = new(108, 116.5, 33.5, 43), ["幽州"] = new(112, 126, 37, 44.5),
        ["交州"] = new(101, 114, 17, 27),
    };

    public static LocationImportResult Import(string repositoryRoot, string dilaXmlPath)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string xmlPath = Path.GetFullPath(dilaXmlPath);
        string actualSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(xmlPath)));
        if (!string.Equals(actualSha, PinnedDilaSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"DILA TEI checksum mismatch: expected {PinnedDilaSha256}, got {actualSha}.");
        }

        SourceDocument source = JsonSerializer.Deserialize<SourceDocument>(
            File.ReadAllText(Path.Combine(root, "data", "research", "later-han-administrative-units.json")), JsonOptions)
            ?? throw new InvalidDataException("Later Han administrative source snapshot is empty.");
        Dictionary<string, SourceRow> byCell = source.Records.ToDictionary(row => row.SourceCell, StringComparer.Ordinal);
        List<DilaPlace> places = ReadDilaPlaces(xmlPath);
        Dictionary<string, List<DilaPlace>> byName = [];
        foreach (DilaPlace place in places)
        {
            foreach (string name in place.Names.Distinct(StringComparer.Ordinal))
            {
                if (!byName.TryGetValue(name, out List<DilaPlace>? named))
                {
                    named = [];
                    byName[name] = named;
                }
                named.Add(place);
            }
        }

        Dictionary<string, ResolvedLocation> resolved = [];
        Dictionary<string, Cluster[]> candidates = [];
        foreach (SourceRow row in source.Records)
        {
            Cluster[] clusters = FindClusters(row, byCell, byName);
            candidates[row.SourceCell] = clusters;
            if (clusters.Length > 0 && clusters[0].Score >= 8
                && (clusters.Length == 1 || clusters[0].Score > clusters[1].Score))
            {
                resolved[row.SourceCell] = FromCluster(row, clusters[0], "dila_direct", clusters[0].Score >= 13 ? "high" : "medium");
            }
        }

        // A commandery is best disambiguated by its independently resolved counties.
        foreach (SourceRow district in source.Records.Where(row => row.Level == "district"))
        {
            ResolvedLocation[] children = source.Records
                .Where(row => row.ParentSourceCell == district.SourceCell && resolved.ContainsKey(row.SourceCell))
                .Select(row => resolved[row.SourceCell]).ToArray();
            (double Longitude, double Latitude)? childCenter = children.Length == 0 ? null : Median(children);
            if (!resolved.ContainsKey(district.SourceCell) && childCenter is not null)
            {
                Cluster? chosen = candidates[district.SourceCell]
                    .OrderBy(cluster => DistanceSquared(cluster.Longitude, cluster.Latitude, childCenter.Value.Longitude, childCenter.Value.Latitude))
                    .FirstOrDefault();
                resolved[district.SourceCell] = chosen is null
                    ? Inferred(district, childCenter.Value.Longitude, childCenter.Value.Latitude, "child_centroid_inferred")
                    : FromCluster(district, chosen, "dila_child_disambiguated", "medium");
            }
        }

        // Ambiguous county names are selected only relative to an already resolved parent commandery.
        foreach (SourceRow locality in source.Records.Where(row => row.Level == "locality" && !resolved.ContainsKey(row.SourceCell)))
        {
            if (!resolved.TryGetValue(locality.ParentSourceCell!, out ResolvedLocation? parent)
                || candidates[locality.SourceCell].Length == 0)
            {
                continue;
            }
            Cluster chosen = candidates[locality.SourceCell]
                .OrderBy(cluster => DistanceSquared(cluster.Longitude, cluster.Latitude, parent.Longitude, parent.Latitude))
                .ThenByDescending(cluster => cluster.Score).First();
            resolved[locality.SourceCell] = FromCluster(locality, chosen, "dila_parent_disambiguated", "medium");
        }

        // Fill the remaining hierarchy deterministically. These are presentation anchors, not claimed coordinates.
        foreach (SourceRow province in source.Records.Where(row => row.Level == "province"))
        {
            ResolvedLocation[] descendants = source.Records
                .Where(row => RegionFor(row, byCell).SourceCell == province.SourceCell && resolved.ContainsKey(row.SourceCell))
                .Select(row => resolved[row.SourceCell]).ToArray();
            if (!resolved.ContainsKey(province.SourceCell) && descendants.Length > 0)
            {
                (double longitude, double latitude) = Median(descendants);
                resolved[province.SourceCell] = Inferred(province, longitude, latitude, "descendant_centroid_inferred");
            }

            SourceRow[] districts = source.Records.Where(row => row.Level == "district" && row.ParentSourceCell == province.SourceCell).ToArray();
            for (int index = 0; index < districts.Length; index++)
            {
                SourceRow district = districts[index];
                if (!resolved.ContainsKey(district.SourceCell))
                {
                    ResolvedLocation parent = resolved[province.SourceCell];
                    (double dx, double dy) = Offset(district.SourceCell, index, 0.7, 0.45);
                    resolved[district.SourceCell] = Inferred(district, parent.Longitude + dx, parent.Latitude + dy, "parent_inferred");
                }
            }
        }

        foreach (SourceRow district in source.Records.Where(row => row.Level == "district"))
        {
            SourceRow[] localities = source.Records.Where(row => row.Level == "locality" && row.ParentSourceCell == district.SourceCell).ToArray();
            for (int index = 0; index < localities.Length; index++)
            {
                SourceRow locality = localities[index];
                if (!resolved.ContainsKey(locality.SourceCell))
                {
                    ResolvedLocation parent = resolved[district.SourceCell];
                    (double dx, double dy) = Offset(locality.SourceCell, index, 0.18, 0.13);
                    resolved[locality.SourceCell] = Inferred(locality, parent.Longitude + dx, parent.Latitude + dy, "parent_inferred");
                }
            }
        }

        LocationRecord[] records = source.Records.Select(row =>
        {
            ResolvedLocation item = resolved[row.SourceCell];
            return new LocationRecord(row.SourceCell, Math.Round(item.Longitude, 6), Math.Round(item.Latitude, 6),
                item.Status, item.Confidence, item.SourcePlaceIds, item.CoordinateCertainties);
        }).ToArray();
        Dictionary<string, int> statusCounts = records.GroupBy(record => record.PlacementStatus)
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        LocationDocument output = new(
            1,
            "later-han-map-anchors-dila-2026-07-13",
            new SourceIdentity(
                "DILA Buddhist Studies Place Authority Database",
                "https://github.com/DILA-edu/Authority-Databases",
                PinnedDilaCommit,
                "authority_place/Buddhist_Studies_Place_Authority.xml",
                PinnedDilaSha256,
                "2008-01-01 to 2026-07-01",
                "CC BY-SA 3.0 Unported",
                "https://creativecommons.org/licenses/by-sa/3.0/",
                "Dharma Drum Institute of Liberal Arts (DILA)"),
            "Traditional-name matching constrained by broad province extents; homonyms use parent/child proximity. Missing coordinates use deterministic parent anchors and are explicitly low-confidence inferred placements.",
            new LocationCounts(records.Length, statusCounts),
            records);
        string outputPath = Path.Combine(root, "data", "research", "later-han-locations.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(output, JsonOptions) + Environment.NewLine);
        return new LocationImportResult(records.Length, statusCounts, outputPath);
    }

    private static List<DilaPlace> ReadDilaPlaces(string path)
    {
        XDocument document = XDocument.Load(path, LoadOptions.None);
        XNamespace xml = XNamespace.Xml;
        List<DilaPlace> result = [];
        foreach (XElement element in document.Descendants().Where(item => item.Name.LocalName == "place" && item.Attribute(xml + "id") is not null))
        {
            XElement? geo = element.Descendants().FirstOrDefault(item => item.Name.LocalName == "geo");
            if (geo is null) continue;
            string[] parts = geo.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)) continue;
            string[] names = element.Elements().Where(item => item.Name.LocalName == "placeName"
                    && item.Attribute(xml + "lang")?.Value == "zho-Hant")
                .Select(item => item.Value.Trim()).Where(name => name.Length > 0).ToArray();
            if (names.Length == 0) continue;
            string country = element.Elements().FirstOrDefault(item => item.Name.LocalName == "country")?.Value.Trim() ?? string.Empty;
            string note = element.Elements().FirstOrDefault(item => item.Name.LocalName == "note" && item.Attribute("type") is null)?.Value ?? string.Empty;
            result.Add(new DilaPlace(element.Attribute(xml + "id")!.Value, names, longitude, latitude,
                geo.Attribute("cert")?.Value ?? "unspecified", country, note));
        }
        return result;
    }

    private static Cluster[] FindClusters(SourceRow row, IReadOnlyDictionary<string, SourceRow> byCell,
        IReadOnlyDictionary<string, List<DilaPlace>> byName)
    {
        Dictionary<string, DilaPlace> matches = [];
        foreach (string name in new[] { row.Hanja, BaseName(row.Hanja) }.Distinct(StringComparer.Ordinal))
        {
            foreach (DilaPlace place in byName.GetValueOrDefault(name) ?? []) matches[place.Id] = place;
        }
        SourceRow region = RegionFor(row, byCell);
        Bounds bounds = RegionBounds[region.Hanja];
        SourceRow? district = row.Level == "locality" ? byCell[row.ParentSourceCell!] : row.Level == "district" ? row : null;
        string[] aliases = region.Hanja == "司隸" ? ["司隸", "司州", "司隸校尉部", "司隸部", "司隸州"] : [region.Hanja];
        ScoredPlace[] scored = matches.Values.Where(place => bounds.Contains(place.Longitude, place.Latitude)).Select(place =>
        {
            int score = place.Names.Contains(row.Hanja, StringComparer.Ordinal) ? 6 : 2;
            if (district is not null && place.Country == district.Hanja) score += 10;
            if (aliases.Contains(place.Country, StringComparer.Ordinal)) score += 8;
            if (place.Note.Contains("東漢", StringComparison.Ordinal) || place.Note.Contains("後漢", StringComparison.Ordinal)) score += 4;
            if (place.Certainty == "high") score++;
            return new ScoredPlace(place, score);
        }).OrderByDescending(item => item.Score).ThenBy(item => item.Place.Id, StringComparer.Ordinal).ToArray();
        List<List<ScoredPlace>> clusters = [];
        foreach (ScoredPlace item in scored)
        {
            List<ScoredPlace>? cluster = clusters.FirstOrDefault(group =>
                Math.Abs(group.Average(entry => entry.Place.Longitude) - item.Place.Longitude) <= 0.12
                && Math.Abs(group.Average(entry => entry.Place.Latitude) - item.Place.Latitude) <= 0.12);
            (cluster ?? AddCluster(clusters)).Add(item);
        }
        return clusters.Select(group => new Cluster(group.Average(item => item.Place.Longitude), group.Average(item => item.Place.Latitude),
                group.Max(item => item.Score) + Math.Min(3, group.Count - 1),
                group.Select(item => item.Place.Id).Order(StringComparer.Ordinal).ToArray(),
                group.Select(item => item.Place.Certainty).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .OrderByDescending(item => item.Score).ThenBy(item => item.SourcePlaceIds[0], StringComparer.Ordinal).ToArray();
    }

    private static List<ScoredPlace> AddCluster(List<List<ScoredPlace>> clusters) { List<ScoredPlace> cluster = []; clusters.Add(cluster); return cluster; }
    private static string BaseName(string value) { string? suffix = RankSuffixes.FirstOrDefault(value.EndsWith); return suffix is null ? value : value[..^suffix.Length]; }
    private static SourceRow RegionFor(SourceRow row, IReadOnlyDictionary<string, SourceRow> byCell) { SourceRow current = row; while (current.Level != "province") current = byCell[current.ParentSourceCell!]; return current; }
    private static ResolvedLocation FromCluster(SourceRow row, Cluster cluster, string status, string confidence) =>
        new(row.SourceCell, cluster.Longitude, cluster.Latitude, status, confidence, cluster.SourcePlaceIds, cluster.CoordinateCertainties);
    private static ResolvedLocation Inferred(SourceRow row, double longitude, double latitude, string status) =>
        new(row.SourceCell, longitude, latitude, status, "low", [], []);
    private static (double Longitude, double Latitude) Median(IReadOnlyList<ResolvedLocation> values) =>
        (Median(values.Select(item => item.Longitude)), Median(values.Select(item => item.Latitude)));
    private static double Median(IEnumerable<double> values) { double[] sorted = values.Order().ToArray(); return sorted.Length % 2 == 0 ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2 : sorted[sorted.Length / 2]; }
    private static double DistanceSquared(double x1, double y1, double x2, double y2) => Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2);
    private static (double X, double Y) Offset(string key, int index, double xScale, double yScale)
    {
        uint hash = 2166136261; foreach (byte value in System.Text.Encoding.UTF8.GetBytes(key)) { hash ^= value; hash *= 16777619; }
        double angle = ((hash % 3600) / 3600d) * Math.PI * 2; double radius = 0.35 + Math.Sqrt(index + 1) * 0.28;
        return (Math.Cos(angle) * radius * xScale, Math.Sin(angle) * radius * yScale);
    }

    private sealed record Bounds(double MinLongitude, double MaxLongitude, double MinLatitude, double MaxLatitude) { public bool Contains(double longitude, double latitude) => longitude >= MinLongitude && longitude <= MaxLongitude && latitude >= MinLatitude && latitude <= MaxLatitude; }
    private sealed record SourceDocument(int SchemaVersion, IReadOnlyList<SourceRow> Records);
    private sealed record SourceRow(string SourceCell, string Level, string? ParentSourceCell, string Hanja);
    private sealed record DilaPlace(string Id, IReadOnlyList<string> Names, double Longitude, double Latitude, string Certainty, string Country, string Note);
    private sealed record ScoredPlace(DilaPlace Place, int Score);
    private sealed record Cluster(double Longitude, double Latitude, int Score, string[] SourcePlaceIds, string[] CoordinateCertainties);
    private sealed record ResolvedLocation(string SourceCell, double Longitude, double Latitude, string Status, string Confidence, string[] SourcePlaceIds, string[] CoordinateCertainties);
    private sealed record SourceIdentity(string Name, string RepositoryUrl, string Commit, string File, string Sha256, string DatasetDate, string License, string LicenseUrl, string Attribution);
    private sealed record LocationCounts(int Total, IReadOnlyDictionary<string, int> ByPlacementStatus);
    private sealed record LocationRecord(string SourceCell, double Longitude, double Latitude, string PlacementStatus, string Confidence, string[] SourcePlaceIds, string[] CoordinateCertainties);
    private sealed record LocationDocument(int SchemaVersion, string DatasetId, SourceIdentity Source, string Method, LocationCounts Counts, IReadOnlyList<LocationRecord> Records);
}

public sealed record LocationImportResult(int Records, IReadOnlyDictionary<string, int> StatusCounts, string OutputPath);
