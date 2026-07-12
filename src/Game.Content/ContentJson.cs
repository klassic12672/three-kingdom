using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Core;

namespace Game.Content;

public static class ContentJson
{
    public static JsonSerializerOptions CreateOptions(bool indented = false)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new EntityIdJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class EntityIdJsonConverter : JsonConverter<EntityId>
    {
        public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("EntityId must be a namespaced string.");
            }

            string? value = reader.GetString();
            if (!EntityId.TryParse(value, out EntityId id))
            {
                throw new JsonException($"'{value}' is not a valid EntityId.");
            }

            return id;
        }

        public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
