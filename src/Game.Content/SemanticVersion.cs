using System.Globalization;
using System.Text.RegularExpressions;

namespace Game.Content;

public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? PreRelease = null) : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SemanticVersion Parse(string value)
    {
        Match match = Pattern.Match(value ?? string.Empty);
        if (!match.Success)
        {
            throw new FormatException($"'{value}' is not a semantic version.");
        }

        return new SemanticVersion(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            match.Groups["pre"].Success ? match.Groups["pre"].Value : null);
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        try
        {
            version = Parse(value ?? string.Empty);
            return true;
        }
        catch (FormatException)
        {
            version = default;
            return false;
        }
    }

    public bool Satisfies(string requirement)
    {
        string trimmed = requirement.Trim();
        string operation = trimmed.StartsWith(">=", StringComparison.Ordinal) ? ">="
            : trimmed.StartsWith("<=", StringComparison.Ordinal) ? "<="
            : trimmed.StartsWith('>') ? ">"
            : trimmed.StartsWith('<') ? "<"
            : trimmed.StartsWith('=') ? "="
            : "=";
        string versionText = operation == "=" && !trimmed.StartsWith('=')
            ? trimmed
            : trimmed[operation.Length..].Trim();
        int comparison = CompareTo(Parse(versionText));
        return operation switch
        {
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            _ => comparison == 0,
        };
    }

    public int CompareTo(SemanticVersion other)
    {
        int comparison = Major.CompareTo(other.Major);
        comparison = comparison != 0 ? comparison : Minor.CompareTo(other.Minor);
        comparison = comparison != 0 ? comparison : Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        if (PreRelease is null)
        {
            return other.PreRelease is null ? 0 : 1;
        }

        return other.PreRelease is null ? -1 : ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}{(PreRelease is null ? string.Empty : $"-{PreRelease}")}";

    private static int ComparePreRelease(string left, string right)
    {
        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        for (int index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            if (index >= leftParts.Length)
            {
                return -1;
            }

            if (index >= rightParts.Length)
            {
                return 1;
            }

            bool leftNumeric = int.TryParse(leftParts[index], CultureInfo.InvariantCulture, out int leftNumber);
            bool rightNumeric = int.TryParse(rightParts[index], CultureInfo.InvariantCulture, out int rightNumber);
            int comparison = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => StringComparer.Ordinal.Compare(leftParts[index], rightParts[index]),
            };
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
