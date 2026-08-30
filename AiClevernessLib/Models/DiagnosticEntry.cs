namespace AiCleverness.Models;

/// <summary>
/// A single diagnostic entry explaining a decision made during execution.
/// </summary>
public sealed record DiagnosticEntry(
    string ExecutionId,
    DiagnosticCategory Category,
    DiagnosticSeverity Severity,
    string Message,
    DateTimeOffset Timestamp,
    string? ComponentName = null,
    string? Detail = null,
    IReadOnlyDictionary<string, string>? Properties = null)
{
    public static DiagnosticEntry Error(string executionId, DiagnosticCategory category, string component, string message, string? detail = null) =>
        new(executionId, category, DiagnosticSeverity.Error, message, DateTimeOffset.UtcNow, component, detail);

    public static DiagnosticEntry Info(string executionId, DiagnosticCategory category, string component, string message, string? detail = null) =>
        new(executionId, category, DiagnosticSeverity.Info, message, DateTimeOffset.UtcNow, component, detail);

    public static DiagnosticEntry Warn(string executionId, DiagnosticCategory category, string component, string message, string? detail = null) =>
        new(executionId, category, DiagnosticSeverity.Warning, message, DateTimeOffset.UtcNow, component, detail);
}
