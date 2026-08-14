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

/// <summary>
/// Category of a diagnostic entry indicating which subsystem produced it.
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>Model or capability resolution decision.</summary>
    ModelSelection,

    /// <summary>Tool selection or invocation decision.</summary>
    ToolSelection,

    /// <summary>Strategy evaluation decision.</summary>
    Strategy,

    /// <summary>Planner selection or plan generation.</summary>
    Planning,

    /// <summary>Policy evaluation decision.</summary>
    Policy,

    /// <summary>Quality gate evaluation.</summary>
    QualityGate,

    /// <summary>Resource allocation or enforcement.</summary>
    Resource,

    /// <summary>General runtime decision.</summary>
    Runtime
}

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
    /// <summary>Creates an error diagnostic entry.</summary>
    public static DiagnosticEntry Error(
        string executionId,
        DiagnosticCategory category,
        string component,
        string message,
        string? detail = null) =>
        new(
            executionId,
            category,
            DiagnosticSeverity.Error,
            message,
            DateTimeOffset.UtcNow,
            component,
            detail);

    /// <summary>Creates an informational diagnostic entry.</summary>
    public static DiagnosticEntry Info(
        string executionId,
        DiagnosticCategory category,
        string component,
        string message,
        string? detail = null) =>
        new(
            executionId,
            category,
            DiagnosticSeverity.Info,
            message,
            DateTimeOffset.UtcNow,
            component,
            detail);

    /// <summary>Creates a warning diagnostic entry.</summary>
    public static DiagnosticEntry Warn(
        string executionId,
        DiagnosticCategory category,
        string component,
        string message,
        string? detail = null) =>
        new(
            executionId,
            category,
            DiagnosticSeverity.Warning,
            message,
            DateTimeOffset.UtcNow,
            component,
            detail);
}

/// <summary>
/// Aggregated diagnostic report for a single execution, grouping entries
/// by category and severity for structured explanation of runtime decisions.
/// </summary>
public sealed record DiagnosticReport
{
    /// <summary>All diagnostic entries, ordered by timestamp.</summary>
    public IReadOnlyList<DiagnosticEntry> Entries { get; init; } = Array.Empty<DiagnosticEntry>();

    /// <summary>Number of error-level entries.</summary>
    public int ErrorCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Error);

    /// <summary>Execution identifier this report belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>UTC timestamp when the report was generated.</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the report contains any warnings or errors.</summary>
    public bool HasIssues => WarningCount > 0 || ErrorCount > 0;

    /// <summary>Number of info-level entries.</summary>
    public int InfoCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Info);

    /// <summary>Number of warning-level entries.</summary>
    public int WarningCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Warning);

    /// <summary>Groups entries by category.</summary>
    public IReadOnlyDictionary<DiagnosticCategory, IReadOnlyList<DiagnosticEntry>> ByCategory()
    {
        return Entries
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DiagnosticEntry>)g.ToList().AsReadOnly());
    }

    /// <summary>Gets entries filtered by category.</summary>
    public IReadOnlyList<DiagnosticEntry> GetByCategory(DiagnosticCategory category) =>
        Entries.Where(e => e.Category == category).ToList().AsReadOnly();

    /// <summary>Gets entries filtered by severity.</summary>
    public IReadOnlyList<DiagnosticEntry> GetBySeverity(DiagnosticSeverity severity) =>
        Entries.Where(e => e.Severity == severity).ToList().AsReadOnly();
}
