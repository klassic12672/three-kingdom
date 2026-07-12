using Simulation.Core;

namespace Game.Content;

public enum ContentDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record ContentDiagnostic(
    ContentDiagnosticSeverity Severity,
    string Code,
    string File,
    string JsonPath,
    EntityId? RecordId,
    string Message,
    string Remediation);

public sealed class ContentValidationReport
{
    private readonly List<ContentDiagnostic> diagnostics = [];

    public IReadOnlyList<ContentDiagnostic> Diagnostics => diagnostics
        .OrderBy(item => item.File, StringComparer.Ordinal)
        .ThenBy(item => item.JsonPath, StringComparer.Ordinal)
        .ThenBy(item => item.Code, StringComparer.Ordinal)
        .ThenBy(item => item.RecordId)
        .ToArray();

    public bool HasErrors => diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error);

    public int ErrorCount => diagnostics.Count(item => item.Severity == ContentDiagnosticSeverity.Error);

    public int WarningCount => diagnostics.Count(item => item.Severity == ContentDiagnosticSeverity.Warning);

    public void Error(
        string code,
        string file,
        string jsonPath,
        EntityId? recordId,
        string message,
        string remediation) => diagnostics.Add(new(
            ContentDiagnosticSeverity.Error,
            code,
            Normalize(file),
            jsonPath,
            recordId,
            message,
            remediation));

    public void Warning(
        string code,
        string file,
        string jsonPath,
        EntityId? recordId,
        string message,
        string remediation) => diagnostics.Add(new(
            ContentDiagnosticSeverity.Warning,
            code,
            Normalize(file),
            jsonPath,
            recordId,
            message,
            remediation));

    public void Append(ContentValidationReport report) => diagnostics.AddRange(report.Diagnostics);

    private static string Normalize(string path) => path.Replace('\\', '/');
}

public sealed class ContentValidationException : Exception
{
    public ContentValidationException(ContentValidationReport report)
        : base($"Content validation failed with {report.ErrorCount} error(s) and {report.WarningCount} warning(s).")
    {
        Report = report;
    }

    public ContentValidationReport Report { get; }
}
