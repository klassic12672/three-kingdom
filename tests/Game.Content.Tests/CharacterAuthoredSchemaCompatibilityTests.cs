using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Game.Content.Tests;

public sealed class CharacterAuthoredSchemaCompatibilityTests
{
    [Fact]
    public void ClosedPublishedSchemaKeepsRuntimeVersionThreeAndEducationStateOutOfAuthoredContent()
    {
        string root = FindRepositoryRoot();
        JsonObject schemaDocument = JsonNode.Parse(File.ReadAllText(
            Path.Combine(root, "data", "schemas", "content-record.schema.json")))!.AsObject();
        schemaDocument["$id"] = "https://three-kingdom.local/schemas/content-record-e6-compatibility-test.schema.json";
        JsonSchema schema = JsonSchema.FromText(schemaDocument.ToJsonString());
        JsonObject authoredVersionTwo = JsonNode.Parse("""
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
                    "contractVersion": 2,
                    "identityDefinitionIds": [],
                    "characterDefinitionIds": ["character:test/one"],
                    "familyDefinitionIds": [],
                    "householdDefinitionIds": [],
                    "characterStates": [
                      {
                        "contractVersion": 2,
                        "characterId": "character:test/one",
                        "parentIds": [],
                        "parentLinks": [],
                        "condition": {
                          "vitalStatus": "alive",
                          "healthStatus": "healthy",
                          "isIncapacitated": false,
                          "custodyStatus": "free",
                          "custodianId": null
                        }
                      }
                    ],
                    "familyStates": [],
                    "householdStates": [],
                    "references": ["character:test/one"]
                  }
                }
              ]
            }
            """)!.AsObject();

        Assert.True(IsValid(schema, authoredVersionTwo));

        JsonObject runtimeWorldVersionThree = (JsonObject)authoredVersionTwo.DeepClone();
        runtimeWorldVersionThree["records"]![0]!["data"]!["contractVersion"] = 3;
        Assert.False(IsValid(schema, runtimeWorldVersionThree));

        JsonObject runtimeStateVersionThree = (JsonObject)authoredVersionTwo.DeepClone();
        runtimeStateVersionThree["records"]![0]!["data"]!["characterStates"]![0]!["contractVersion"] = 3;
        Assert.False(IsValid(schema, runtimeStateVersionThree));

        JsonObject educationInjection = (JsonObject)authoredVersionTwo.DeepClone();
        educationInjection["records"]![0]!["data"]!["characterStates"]![0]!["educationAttainments"] =
            new JsonArray();
        Assert.False(IsValid(schema, educationInjection));
    }

    private static bool IsValid(JsonSchema schema, JsonNode instance) =>
        schema.Evaluate(JsonSerializer.SerializeToElement(instance)).IsValid;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
