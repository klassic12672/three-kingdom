using Game.Content;
using Simulation.Core;

namespace Game.Content.Tests;

public sealed class ContentValidationTests
{
    [Fact]
    public void MalformedModFailsIndependentlyWithoutCorruptingBuiltInPack()
    {
        using ContentPackFixture fixture = new();
        string valid = fixture.WritePack(
            new EntityId("pack:base"),
            builtIn: true,
            records: [ContentPackFixture.FictionalRecord("record:base")]);
        string invalidDirectory = Path.Combine(fixture.Root, "invalid");
        Directory.CreateDirectory(invalidDirectory);
        string invalid = Path.Combine(invalidDirectory, "content-manifest.json");
        File.WriteAllText(invalid, "{\"schemaVersion\":1,\"packId\":\"pack:broken\"}\n");

        ContentLoadResult result = new ContentPackLoader().Load([invalid, valid], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "manifest.schema");
        Assert.Equal([new EntityId("pack:base")], result.LoadOrder.Select(pack => pack.Manifest.PackId));
        Assert.True(result.Registry.TryGet(new EntityId("record:base"), out _));
    }

    [Fact]
    public void MissingReleaseTranslationReportsEveryLocaleGap()
    {
        using ContentPackFixture fixture = new();
        ContentRecord record = ContentPackFixture.FictionalRecord("record:release", releaseMarked: true) with
        {
            LocalizationKeys = [new EntityId("loc:release/name")],
        };
        string manifest = fixture.WritePack(new EntityId("pack:missing_translation"), records: [record]);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Equal(2, result.Report.Diagnostics.Count(item => item.Code == "localization.coverage" && item.Severity == ContentDiagnosticSeverity.Error));
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void UnknownLocalizationVariableRejectsPack()
    {
        using ContentPackFixture fixture = new();
        string csv =
            "key,locale,text,context,variables,review_state,source_content_ids,release_marked\n" +
            "loc:test/message,en-US,Count {count},Test message,,approved,,false\n";
        string manifest = fixture.WritePack(new EntityId("pack:bad_variables"), localizationCsv: csv);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "localization.variables");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void BrokenPluralBranchAndMarkupRejectPack()
    {
        using ContentPackFixture fixture = new();
        string csv =
            "key,locale,text,context,variables,review_state,source_content_ids,release_marked\n" +
            "loc:test/plural,en-US,\"{count, plural, one {[b]one[/i]}}\",Test plural,count,approved,,false\n";
        string manifest = fixture.WritePack(new EntityId("pack:bad_markup"), localizationCsv: csv);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "localization.other_branch");
        Assert.Contains(result.Report.Diagnostics, item => item.Code == "localization.markup");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void InconsistentPluralBranchesAcrossLocalesRejectPack()
    {
        using ContentPackFixture fixture = new();
        string csv =
            "key,locale,text,context,variables,review_state,source_content_ids,release_marked\n" +
            "loc:test/count,en-US,\"{count, plural, one {one item} other {many items}}\",Count,count,approved,,false\n" +
            "loc:test/count,ko-KR,\"{count, plural, other {여러 개}}\",Count,count,approved,,false\n";
        string manifest = fixture.WritePack(new EntityId("pack:branch_mismatch"), localizationCsv: csv);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "localization.branches");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void GlossaryRequiresBothLaunchLanguages()
    {
        using ContentPackFixture fixture = new();
        string csv =
            "term_id,ko-KR,en-US,notes,review_state\n" +
            "term:test/office,태수,,Missing English,approved\n";
        string manifest = fixture.WritePack(new EntityId("pack:bad_glossary"), glossaryCsv: csv);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "glossary.coverage");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void InvalidUtf8LocalizationIsRejected()
    {
        using ContentPackFixture fixture = new();
        byte[] invalidUtf8 = [0x6b, 0x65, 0x79, 0x0a, 0xc3, 0x28];
        string manifest = fixture.WritePack(new EntityId("pack:invalid_utf8"), localizationBytes: invalidUtf8);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "localization.csv");
        Assert.Empty(result.LoadOrder);
    }

    [Theory]
    [InlineData("region", "population", -1, "record.range")]
    [InlineData("character", "birthYear", 0, "record.date")]
    public void InitialTypedRecordsValidateRanges(
        string recordType,
        string field,
        int value,
        string code)
    {
        using ContentPackFixture fixture = new();
        ContentRecord record = ContentPackFixture.FictionalRecord("record:bad_typed") with
        {
            RecordType = recordType,
            Data = new System.Text.Json.Nodes.JsonObject
            {
                [field] = value,
                ["references"] = new System.Text.Json.Nodes.JsonArray(),
            },
        };
        string manifest = fixture.WritePack(new EntityId("pack:bad_typed"), records: [record]);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == code);
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void InvalidProlepticEventDateIsRejected()
    {
        using ContentPackFixture fixture = new();
        ContentRecord record = ContentPackFixture.FictionalRecord("record:bad_date") with
        {
            RecordType = "dated_event",
            Data = new System.Text.Json.Nodes.JsonObject
            {
                ["date"] = "1900-02-29",
                ["references"] = new System.Text.Json.Nodes.JsonArray(),
            },
        };
        string manifest = fixture.WritePack(new EntityId("pack:bad_date"), records: [record]);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "record.date");
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void HistoricalAndDisputedRecordsRequireSourceEvidence()
    {
        using ContentPackFixture fixture = new();
        ContentRecord historical = ContentPackFixture.FictionalRecord("record:historical") with
        {
            ContentTag = ContentTag.Historical,
        };
        ContentRecord disputed = ContentPackFixture.FictionalRecord("record:disputed") with
        {
            ContentTag = ContentTag.Disputed,
            SourceIds = [new EntityId("source:one")],
        };
        string manifest = fixture.WritePack(new EntityId("pack:bad_sources"), records: [historical, disputed]);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "record.sources");
        Assert.Contains(result.Report.Diagnostics, item => item.Code == "record.disputed_sources");
        Assert.Empty(result.LoadOrder);
    }

    [Theory]
    [InlineData(AssetOrigin.LiveGenerated, ContentClassification.General, "release.live_generation")]
    [InlineData(AssetOrigin.Human, ContentClassification.SexuallyExplicit, "release.explicit_asset")]
    [InlineData(AssetOrigin.OfflineAi, ContentClassification.General, "provenance.incomplete")]
    public void ReleaseManifestRejectsUnsafeProvenance(
        AssetOrigin origin,
        ContentClassification classification,
        string expectedCode)
    {
        using ContentPackFixture fixture = new();
        AssetProvenance asset = new(
            1,
            new EntityId("provenance:test/asset"),
            [],
            origin,
            "test rights",
            null,
            null,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            new string('a', 64),
            false,
            true,
            classification);
        string manifest = fixture.WritePack(
            new EntityId("pack:unsafe_asset"),
            assets: [asset],
            releaseEligible: true);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == expectedCode);
        Assert.Empty(result.LoadOrder);
    }

    [Fact]
    public void ExplicitReleaseRecordIsRejected()
    {
        using ContentPackFixture fixture = new();
        ContentRecord record = ContentPackFixture.FictionalRecord("record:explicit", releaseMarked: true) with
        {
            Classification = ContentClassification.SexuallyExplicit,
        };
        string manifest = fixture.WritePack(new EntityId("pack:explicit"), records: [record]);

        ContentLoadResult result = new ContentPackLoader().Load([manifest], "0.1.0", fixture.Root);

        Assert.Contains(result.Report.Diagnostics, item => item.Code == "release.explicit_content");
        Assert.Empty(result.LoadOrder);
    }
}
