using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Simulation.Core;

public readonly record struct EntityId : IComparable<EntityId>
{
    [JsonConstructor]
    public EntityId(string value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException("Entity IDs must use lowercase 'namespace:value' syntax.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    [JsonIgnore]
    public bool IsValid => IsValidValue(Value);

    public int CompareTo(EntityId other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;

    public static EntityId Parse(string value) => new(value);

    public static bool TryParse(string? value, [NotNullWhen(true)] out EntityId id)
    {
        if (value is not null && IsValidValue(value))
        {
            id = new EntityId(value);
            return true;
        }

        id = default;
        return false;
    }

    private static bool IsValidValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
        {
            return false;
        }

        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator < 1 || separator == value.Length - 1 || value.LastIndexOf(':') != separator)
        {
            return false;
        }

        return IsSegment(value.AsSpan(0, separator), allowSlash: false)
            && IsSegment(value.AsSpan(separator + 1), allowSlash: true);
    }

    private static bool IsSegment(ReadOnlySpan<char> segment, bool allowSlash)
    {
        if (segment.IsEmpty || segment[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (char character in segment)
        {
            bool valid = character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_' or '-' or '.'
                || (allowSlash && character == '/');
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
