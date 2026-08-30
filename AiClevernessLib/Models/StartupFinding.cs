namespace AiCleverness.Models;

/// <summary>A single finding from the startup analysis.</summary>
public sealed record StartupFinding(
    string ServiceName,
    StartupSeverity Severity,
    string Message,
    string? Suggestion = null)
{
    public RuntimeValidationCategory Category { get; init; } = RuntimeValidationCategory.DiGraph;
}
