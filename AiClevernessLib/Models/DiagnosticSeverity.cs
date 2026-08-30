namespace AiCleverness.Models;

/// <summary>
/// Severity level of a diagnostic entry.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational note about a decision.</summary>
    Info,

    /// <summary>Warning about a sub-optimal or unexpected condition.</summary>
    Warning,

    /// <summary>Error indicating a failed decision or resolution.</summary>
    Error
}
