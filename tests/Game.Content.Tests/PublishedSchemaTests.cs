using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Json.Schema;

namespace Game.Content.Tests;

public sealed class PublishedSchemaTests
{
    [Fact]
    public void InitialPublishedSchemasAreValidDraft202012Documents()
    {
        string root = FindRepositoryRoot();
        string schemaDirectory = Path.Combine(root, "data", "schemas");
        string[] expected =
        [
            "asset-provenance.schema.json",
            "content-manifest.schema.json",
            "content-override.schema.json",
            "content-record.schema.json",
            "localization-entry.schema.json",
            "source-reference.schema.json",
        ];

        foreach (string file in expected)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaDirectory, file)));
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", document.RootElement.GetProperty("$schema").GetString());
            Assert.Equal("object", document.RootElement.GetProperty("type").GetString());
            Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void ManifestContractHasNoRuntimeCodeOrScriptFileKind()
    {
        string root = FindRepositoryRoot();
        string schema = File.ReadAllText(Path.Combine(root, "data", "schemas", "content-manifest.schema.json"));
        string source = string.Join(
            '\n',
            Directory.EnumerateFiles(Path.Combine(root, "src", "Game.Content"), "*.cs")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("script", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assembly.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CSharpCompilation", source, StringComparison.Ordinal);
        Assert.Equal(
            ["glossary", "localization", "overrides", "provenance", "records", "sources"],
            Enum.GetValues<ContentFileKind>().Select(value => value.ToString().ToLowerInvariant()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ContentRecordSchemaPublishesClosedVersionOneAndTwoCharacterPayloadShapes()
    {
        string root = FindRepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "data", "schemas", "content-record.schema.json")));
        JsonElement schema = document.RootElement;
        JsonElement item = schema.GetProperty("properties").GetProperty("records").GetProperty("items");
        (string RecordType, string DataReference)[] discriminators = item.GetProperty("allOf")
            .EnumerateArray()
            .Select(branch => (
                branch.GetProperty("if")
                    .GetProperty("properties")
                    .GetProperty("recordType")
                    .GetProperty("const")
                    .GetString()!,
                branch.GetProperty("then")
                    .GetProperty("properties")
                    .GetProperty("data")
                    .GetProperty("$ref")
                    .GetString()!))
            .OrderBy(item => item.Item1, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                ("character_definition", "#/$defs/characterDefinitionData"),
                ("character_identity_definition", "#/$defs/characterIdentityDefinitionData"),
                ("character_world", "#/$defs/characterWorldData"),
                ("family_definition", "#/$defs/namedCharacterDefinitionData"),
                ("household_definition", "#/$defs/namedCharacterDefinitionData"),
            ],
            discriminators);

        JsonElement definitions = schema.GetProperty("$defs");
        (string Union, string Legacy, string Current)[] expectedDefinitions =
        [
            ("characterDefinitionData", "legacyCharacterDefinitionData", "currentCharacterDefinitionData"),
            ("characterIdentityDefinitionData", "legacyCharacterIdentityDefinitionData", "currentCharacterIdentityDefinitionData"),
            ("characterWorldData", "legacyCharacterWorldData", "currentCharacterWorldData"),
            ("namedCharacterDefinitionData", "legacyNamedCharacterDefinitionData", "currentNamedCharacterDefinitionData"),
        ];
        foreach ((string union, string legacy, string current) in expectedDefinitions)
        {
            Assert.Equal(
                [$"#/$defs/{legacy}", $"#/$defs/{current}"],
                definitions.GetProperty(union).GetProperty("oneOf")
                    .EnumerateArray()
                    .Select(branch => branch.GetProperty("$ref").GetString()));
            AssertContract(legacy, 1);
            AssertContract(current, 2);
        }

        JsonSchema publishedSchema = JsonSchema.FromFile(
            Path.Combine(root, "data", "schemas", "content-record.schema.json"));
        JsonObject validDocument = CreateRepresentativeCharacterDocument();
        Assert.True(IsValid(publishedSchema, validDocument));
        JsonObject validV2Document = CreateRepresentativeV2CharacterDocument();
        Assert.True(IsValid(publishedSchema, validV2Document));

        JsonObject invalidWorld = (JsonObject)validDocument.DeepClone();
        invalidWorld["records"]![0]!["data"]!["characterStates"]!.AsArray().Add(JsonNode.Parse("""
            {
              "contractVersion": 1,
              "characterId": "not-an-entity-id",
              "parentIds": []
            }
            """));
        Assert.False(IsValid(publishedSchema, invalidWorld));

        JsonObject invalidCharacter = (JsonObject)validDocument.DeepClone();
        invalidCharacter["records"]![1]!["data"]!.AsObject().Remove("birthDate");
        Assert.False(IsValid(publishedSchema, invalidCharacter));

        JsonObject invalidFamily = (JsonObject)validDocument.DeepClone();
        invalidFamily["records"]![2]!["data"]!["futureField"] = true;
        Assert.False(IsValid(publishedSchema, invalidFamily));

        JsonObject invalidHousehold = (JsonObject)validDocument.DeepClone();
        invalidHousehold["records"]![3]!["data"]!["contractVersion"] = 3;
        Assert.False(IsValid(publishedSchema, invalidHousehold));

        JsonObject invalidIdentity = (JsonObject)validDocument.DeepClone();
        invalidIdentity["records"]![4]!["data"]!["kind"] = "military_skill";
        Assert.False(IsValid(publishedSchema, invalidIdentity));

        JsonObject missingV2Condition = (JsonObject)validV2Document.DeepClone();
        missingV2Condition["records"]![0]!["data"]!["characterStates"]![0]!
            .AsObject().Remove("condition");
        Assert.False(IsValid(publishedSchema, missingV2Condition));

        JsonObject nullV2Flaws = (JsonObject)validV2Document.DeepClone();
        nullV2Flaws["records"]![1]!["data"]!["flawIds"] = null;
        Assert.False(IsValid(publishedSchema, nullV2Flaws));

        JsonObject unsupportedV2Origin = (JsonObject)validV2Document.DeepClone();
        unsupportedV2Origin["records"]![1]!["data"]!["originKind"] = "generated";
        Assert.False(IsValid(publishedSchema, unsupportedV2Origin));

        void AssertContract(string name, int version)
        {
            JsonElement definition = definitions.GetProperty(name);
            Assert.Equal("object", definition.GetProperty("type").GetString());
            Assert.False(definition.GetProperty("additionalProperties").GetBoolean());
            Assert.Contains("contractVersion", definition.GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString()));
            Assert.Equal(version, definition.GetProperty("properties")
                .GetProperty("contractVersion")
                .GetProperty("const")
                .GetInt32());
        }
    }

    private static bool IsValid(JsonSchema schema, JsonNode instance)
    {
        return schema.Evaluate(JsonSerializer.SerializeToElement(instance)).IsValid;
    }

    private static JsonObject CreateRepresentativeCharacterDocument()
    {
        return JsonNode.Parse("""
            {
              "schemaVersion": 1,
              "records": [
                {
                  "schemaVersion": 1,
                  "id": "character_world:test",
                  "recordType": "character_world",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": [],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 1,
                    "identityDefinitionIds": [],
                    "characterDefinitionIds": [],
                    "familyDefinitionIds": [],
                    "householdDefinitionIds": [],
                    "characterStates": [],
                    "familyStates": [],
                    "householdStates": [],
                    "references": []
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "character:test",
                  "recordType": "character_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:character/test/name"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 1,
                    "nameKey": "loc:character/test/name",
                    "birthDate": { "year": 190, "month": 1, "day": 2 },
                    "abilityIds": [],
                    "aptitudeIds": [],
                    "traitIds": [],
                    "ambitionIds": [],
                    "reputationIds": [],
                    "references": []
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "family:test",
                  "recordType": "family_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:family/test/name"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 1,
                    "nameKey": "loc:family/test/name",
                    "references": []
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "household:test",
                  "recordType": "household_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:household/test/name"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 1,
                    "nameKey": "loc:household/test/name",
                    "references": []
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "ability:test",
                  "recordType": "character_identity_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:ability/test/name"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 1,
                    "kind": "ability",
                    "nameKey": "loc:ability/test/name",
                    "references": []
                  }
                }
              ]
            }
            """)!.AsObject();
    }

    private static JsonObject CreateRepresentativeV2CharacterDocument()
    {
        return JsonNode.Parse("""
            {
              "schemaVersion": 1,
              "records": [
                {
                  "schemaVersion": 1,
                  "id": "character_world:test_v2",
                  "recordType": "character_world",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": [],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 2,
                    "identityDefinitionIds": ["flaw:test"],
                    "characterDefinitionIds": ["character:test_v2"],
                    "familyDefinitionIds": [],
                    "householdDefinitionIds": [],
                    "characterStates": [{
                      "contractVersion": 2,
                      "characterId": "character:test_v2",
                      "parentIds": [],
                      "parentLinks": [],
                      "condition": {
                        "vitalStatus": "alive",
                        "healthStatus": "healthy",
                        "isIncapacitated": false,
                        "custodyStatus": "free",
                        "custodianId": null
                      }
                    }],
                    "familyStates": [],
                    "householdStates": [],
                    "references": ["character:test_v2", "flaw:test"]
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "character:test_v2",
                  "recordType": "character_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:character/test/name", "loc:character/test/style"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 2,
                    "nameKey": "loc:character/test/name",
                    "courtesyNameKey": "loc:character/test/style",
                    "originKind": "custom",
                    "cultureId": null,
                    "originLocationId": null,
                    "birthDate": { "year": 190, "month": 1, "day": 2 },
                    "abilityIds": [],
                    "aptitudeIds": [],
                    "traitIds": [],
                    "ambitionIds": [],
                    "reputationIds": [],
                    "flawIds": ["flaw:test"],
                    "references": ["flaw:test"]
                  }
                },
                {
                  "schemaVersion": 1,
                  "id": "flaw:test",
                  "recordType": "character_identity_definition",
                  "contentTag": "fictional",
                  "classification": "general",
                  "sourceIds": [],
                  "localizationKeys": ["loc:flaw/test/name"],
                  "releaseMarked": false,
                  "data": {
                    "contractVersion": 2,
                    "kind": "flaw",
                    "nameKey": "loc:flaw/test/name",
                    "references": []
                  }
                }
              ]
            }
            """)!.AsObject();
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
