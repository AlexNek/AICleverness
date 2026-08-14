namespace AiCleverness.Models;

/// <summary>
/// Result of a startup analysis that validates required services are registered.
/// </summary>
public sealed record StartupAnalysisResult
{
    /// <summary>UTC timestamp when the analysis was performed.</summary>
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of errors found.</summary>
    public int ErrorCount => Findings.Count(f => f.Severity == StartupSeverity.Error);

    /// <summary>Individual analysis findings.</summary>
    public IReadOnlyList<StartupFinding> Findings { get; init; } = Array.Empty<StartupFinding>();

    /// <summary>Number of informational notes.</summary>
    public int InfoCount => Findings.Count(f => f.Severity == StartupSeverity.Info);

    /// <summary>Whether the analysis passed with no errors.</summary>
    public bool IsHealthy => !Findings.Any(f => f.Severity == StartupSeverity.Error);

    /// <summary>Number of warnings found.</summary>
    public int WarningCount => Findings.Count(f => f.Severity == StartupSeverity.Warning);

    /// <summary>
    /// Returns only error-severity findings.
    /// </summary>
    public IReadOnlyList<StartupFinding> GetErrors() =>
        Findings.Where(f => f.Severity == StartupSeverity.Error).ToList().AsReadOnly();

    /// <summary>
    /// Returns only findings for the specified validation category.
    /// </summary>
    public IReadOnlyList<StartupFinding> GetFindings(RuntimeValidationCategory category) =>
        Findings.Where(f => f.Category == category).ToList().AsReadOnly();

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if any error-severity findings exist.
    /// The exception message contains all errors grouped by category.
    /// </summary>
    public void ThrowOnErrors()
    {
        var errors = Findings.Where(f => f.Severity == StartupSeverity.Error).ToList();
        if (errors.Count == 0) return;

        var grouped = errors.GroupBy(f => f.Category);
        var lines = new List<string> { "AiCleverness startup validation failed:" };
        foreach (var group in grouped)
        {
            lines.Add($"  [{group.Key}]");
            foreach (var e in group)
                lines.Add($"    - {e.Message}  {e.Suggestion}");
        }

        throw new InvalidOperationException(string.Join(Environment.NewLine, lines));
    }
}

/// <summary>
/// A single finding from the startup analysis.
/// </summary>
public sealed record StartupFinding(
    string ServiceName,
    StartupSeverity Severity,
    string Message,
    string? Suggestion = null)
{
    /// <summary>Validation category this finding belongs to.</summary>
    public RuntimeValidationCategory Category { get; init; } = RuntimeValidationCategory.DiGraph;
}

/// <summary>
/// Severity level for startup analysis findings.
/// </summary>
public enum StartupSeverity
{
    /// <summary>Informational note.</summary>
    Info,

    /// <summary>Warning about a potentially missing or misconfigured service.</summary>
    Warning,

    /// <summary>Error indicating a required service is missing.</summary>
    Error
}

/// <summary>
/// Categories of runtime validation performed by the startup analyzer.
/// </summary>
public enum RuntimeValidationCategory
{
    /// <summary>DI container graph validation (required and recommended services).</summary>
    DiGraph,

    /// <summary>Tool registration validation (names, definitions, collisions).</summary>
    Tools,

    /// <summary>Workflow definition validation (nodes, edges, entry points).</summary>
    Workflows,

    /// <summary>Approval configuration validation.</summary>
    Approval,

    /// <summary>Persistence configuration validation.</summary>
    Persistence,

    /// <summary>Event bus and observer configuration.</summary>
    Observability
}
