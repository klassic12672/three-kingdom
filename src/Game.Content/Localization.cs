using System.Text.RegularExpressions;
using Simulation.Core;

namespace Game.Content;

internal static partial class Localization
{
    private static readonly string[] RequiredHeaders =
    [
        "key", "locale", "text", "context", "variables", "review_state", "source_content_ids", "release_marked",
    ];

    public static IReadOnlyList<LocalizationEntry> ReadEntries(
        string path,
        ContentValidationReport report,
        string? diagnosticPath = null)
    {
        string file = diagnosticPath ?? path;
        try
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = CsvTable.Read(path);
            List<LocalizationEntry> entries = [];
            for (int index = 0; index < rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = rows[index];
                if (RequiredHeaders.Any(header => !row.ContainsKey(header)))
                {
                    report.Error(
                        "localization.headers",
                        file,
                        "$",
                        null,
                        "Localization CSV is missing required headers.",
                        $"Use these headers: {string.Join(",", RequiredHeaders)}.");
                    return [];
                }

                if (!EntityId.TryParse(row["key"], out EntityId key)
                    || !Enum.TryParse(row["review_state"], ignoreCase: true, out LocalizationReviewState reviewState)
                    || !bool.TryParse(row["release_marked"], out bool releaseMarked))
                {
                    report.Error(
                        "localization.row_shape",
                        file,
                        $"$[{index + 2}]",
                        null,
                        "Localization row contains an invalid key, review state, or release flag.",
                        "Use a namespaced key, draft/reviewed/approved, and true/false.");
                    continue;
                }

                EntityId[] sources = ParseIds(row["source_content_ids"], file, index, report);
                LocalizationEntry entry = new(
                    ContentContractVersions.Localization,
                    key,
                    row["locale"],
                    row["text"],
                    row["context"],
                    Split(row["variables"]),
                    reviewState,
                    sources,
                    releaseMarked);
                ValidateEntry(entry, file, index + 2, report);
                entries.Add(entry);
            }

            return entries;
        }
        catch (InvalidDataException exception)
        {
            report.Error("localization.csv", file, "$", null, exception.Message, "Repair the UTF-8 RFC 4180 CSV file.");
            return [];
        }
    }

    public static IReadOnlyList<GlossaryEntry> ReadGlossary(
        string path,
        ContentValidationReport report,
        string? diagnosticPath = null)
    {
        string file = diagnosticPath ?? path;
        try
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = CsvTable.Read(path);
            string[] headers = ["term_id", "ko-KR", "en-US", "notes", "review_state"];
            if (rows.Any(row => headers.Any(header => !row.ContainsKey(header))))
            {
                report.Error("glossary.headers", file, "$", null, "Glossary headers are invalid.", $"Use: {string.Join(",", headers)}.");
                return [];
            }

            List<GlossaryEntry> entries = [];
            for (int index = 0; index < rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = rows[index];
                if (!EntityId.TryParse(row["term_id"], out EntityId id)
                    || !Enum.TryParse(row["review_state"], true, out LocalizationReviewState state))
                {
                    report.Error("glossary.row_shape", file, $"$[{index + 2}]", null, "Glossary row is invalid.", "Use a namespaced term ID and valid review state.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row["ko-KR"]) || string.IsNullOrWhiteSpace(row["en-US"]))
                {
                    report.Error("glossary.coverage", file, $"$[{index + 2}]", id, "Glossary term is missing Korean or English text.", "Supply both launch-language terms.");
                }

                entries.Add(new(id, row["ko-KR"], row["en-US"], row["notes"], state));
            }

            return entries;
        }
        catch (InvalidDataException exception)
        {
            report.Error("glossary.csv", file, "$", null, exception.Message, "Repair the UTF-8 RFC 4180 glossary CSV.");
            return [];
        }
    }

    private static void ValidateEntry(
        LocalizationEntry entry,
        string path,
        int row,
        ContentValidationReport report)
    {
        string jsonPath = $"$[{row}]";
        if (entry.Locale is not ("ko-KR" or "en-US"))
        {
            report.Error("localization.locale", path, jsonPath, entry.Key, $"Unsupported locale '{entry.Locale}'.", "Use ko-KR or en-US for launch content.");
        }

        if (string.IsNullOrWhiteSpace(entry.Text))
        {
            report.Error("localization.empty", path, jsonPath, entry.Key, "Localization text is empty.", "Provide translated text.");
        }

        string[] usedVariables = VariablePattern().Matches(entry.Text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] declaredVariables = entry.Variables.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!usedVariables.SequenceEqual(declaredVariables, StringComparer.Ordinal))
        {
            report.Error(
                "localization.variables",
                path,
                jsonPath,
                entry.Key,
                $"Text variables [{string.Join(", ", usedVariables)}] do not match declarations [{string.Join(", ", declaredVariables)}].",
                "Declare exactly the variables used by the message.");
        }

        if (!HasBalancedBraces(entry.Text))
        {
            report.Error("localization.markup", path, jsonPath, entry.Key, "Message braces are unbalanced.", "Balance every message-format opening and closing brace.");
        }

        if ((entry.Text.Contains(", plural,", StringComparison.Ordinal)
            || entry.Text.Contains(", select,", StringComparison.Ordinal))
            && !OtherBranchPattern().IsMatch(entry.Text))
        {
            report.Error("localization.other_branch", path, jsonPath, entry.Key, "Plural/select message is missing an 'other' branch.", "Add an other {...} branch.");
        }

        MatchCollection markup = MarkupPattern().Matches(entry.Text);
        Stack<string> tags = [];
        foreach (Match match in markup)
        {
            string tag = match.Groups["tag"].Value;
            if (match.Groups["close"].Success)
            {
                if (!tags.TryPop(out string? open) || !StringComparer.Ordinal.Equals(open, tag))
                {
                    report.Error("localization.markup", path, jsonPath, entry.Key, "Rich-text markup is not properly nested.", "Close rich-text tags in reverse opening order.");
                    break;
                }
            }
            else
            {
                tags.Push(tag);
            }
        }

        if (tags.Count > 0)
        {
            report.Error("localization.markup", path, jsonPath, entry.Key, "Rich-text markup has an unclosed tag.", "Close every [b], [i], and [color] tag.");
        }

        string withoutSupportedMarkup = MarkupPattern().Replace(entry.Text, string.Empty);
        if (UnknownMarkupPattern().IsMatch(withoutSupportedMarkup))
        {
            report.Error("localization.markup", path, jsonPath, entry.Key, "Message contains an unsupported rich-text tag.", "Use only [b], [i], and [color] tags in initial content.");
        }
    }

    public static string BranchSignature(string text) => string.Join(
        '|',
        BranchPattern().Matches(text)
            .Select(match => match.Groups["branch"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

    private static bool HasBalancedBraces(string text)
    {
        int depth = 0;
        foreach (char character in text)
        {
            depth += character == '{' ? 1 : character == '}' ? -1 : 0;
            if (depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static EntityId[] ParseIds(
        string value,
        string path,
        int row,
        ContentValidationReport report)
    {
        List<EntityId> ids = [];
        foreach (string item in Split(value))
        {
            if (EntityId.TryParse(item, out EntityId id))
            {
                ids.Add(id);
            }
            else
            {
                report.Error("localization.source_id", path, $"$[{row + 2}]", null, $"'{item}' is not a valid source content ID.", "Use namespaced IDs separated by '|'.");
            }
        }

        return ids.ToArray();
    }

    private static string[] Split(string value) => value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"\{(?<name>[a-zA-Z_][a-zA-Z0-9_]*)(?:[,}])", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"other\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex OtherBranchPattern();

    [GeneratedRegex(@"\[(?<close>/)?(?<tag>b|i|color)(?:=[^\]]+)?\]", RegexOptions.CultureInvariant)]
    private static partial Regex MarkupPattern();

    [GeneratedRegex(@"\[/?[A-Za-z][^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex UnknownMarkupPattern();

    [GeneratedRegex(@"(?<branch>=[0-9]+|zero|one|two|few|many|other)\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex BranchPattern();
}
