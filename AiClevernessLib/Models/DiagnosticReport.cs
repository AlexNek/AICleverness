namespace AiCleverness.Models;

/// <summary>
/// Aggregated diagnostic report for a single execution, grouping entries
/// by category and severity for structured explanation of runtime decisions.
/// </summary>
public sealed record DiagnosticReport
{
    public IReadOnlyList<DiagnosticEntry> Entries { get; init; } = Array.Empty<DiagnosticEntry>();
    public int ErrorCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Error);
    public required string ExecutionId { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool HasIssues => WarningCount > 0 || ErrorCount > 0;
    public int InfoCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Info);
    public int WarningCount => Entries.Count(e => e.Severity == DiagnosticSeverity.Warning);

    public IReadOnlyDictionary<DiagnosticCategory, IReadOnlyList<DiagnosticEntry>> ByCategory() =>
        Entries.GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DiagnosticEntry>)g.ToList().AsReadOnly());

    public IReadOnlyList<DiagnosticEntry> GetByCategory(DiagnosticCategory category) =>
        Entries.Where(e => e.Category == category).ToList().AsReadOnly();

    public IReadOnlyList<DiagnosticEntry> GetBySeverity(DiagnosticSeverity severity) =>
        Entries.Where(e => e.Severity == severity).ToList().AsReadOnly();
}
