using System.Text;

namespace Game.Content;

internal static class CsvTable
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string path)
    {
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(File.ReadAllBytes(path));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"CSV file '{path}' is not valid UTF-8.", exception);
        }

        IReadOnlyList<IReadOnlyList<string>> rows = Parse(text);
        if (rows.Count == 0)
        {
            return [];
        }

        string[] headers = rows[0].Select(header => header.Trim()).ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace)
            || headers.Distinct(StringComparer.Ordinal).Count() != headers.Length)
        {
            throw new InvalidDataException($"CSV file '{path}' has empty or duplicate headers.");
        }

        List<IReadOnlyDictionary<string, string>> records = [];
        for (int index = 1; index < rows.Count; index++)
        {
            IReadOnlyList<string> row = rows[index];
            if (row.Count == 1 && row[0].Length == 0)
            {
                continue;
            }

            if (row.Count != headers.Length)
            {
                throw new InvalidDataException(
                    $"CSV file '{path}' row {index + 1} has {row.Count} fields; expected {headers.Length}.");
            }

            records.Add(headers.Select((header, field) => (header, value: row[field]))
                .ToDictionary(item => item.header, item => item.value, StringComparer.Ordinal));
        }

        return records;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        List<IReadOnlyList<string>> rows = [];
        List<string> row = [];
        StringBuilder field = new();
        bool quoted = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    FinishRow();
                    break;
                case '\n':
                    FinishRow();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("CSV contains an unterminated quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            FinishRow();
        }

        return rows;

        void FinishRow()
        {
            row.Add(field.ToString());
            field.Clear();
            rows.Add(row.ToArray());
            row.Clear();
        }
    }
}
