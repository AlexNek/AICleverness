namespace AiCleverness.Models;

/// <summary>Result of a startup analysis that validates required services are registered.</summary>
public sealed record StartupAnalysisResult
{
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.UtcNow;
    public int ErrorCount => Findings.Count(f => f.Severity == StartupSeverity.Error);
    public IReadOnlyList<StartupFinding> Findings { get; init; } = Array.Empty<StartupFinding>();
    public int InfoCount => Findings.Count(f => f.Severity == StartupSeverity.Info);
    public bool IsHealthy => !Findings.Any(f => f.Severity == StartupSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == StartupSeverity.Warning);
    public IReadOnlyList<StartupFinding> GetErrors() => Findings.Where(f => f.Severity == StartupSeverity.Error).ToList().AsReadOnly();
    public IReadOnlyList<StartupFinding> GetFindings(RuntimeValidationCategory category) => Findings.Where(f => f.Category == category).ToList().AsReadOnly();

    public void ThrowOnErrors()
    {
        var errors = Findings.Where(f => f.Severity == StartupSeverity.Error).ToList();
        if (errors.Count == 0) return;
        var grouped = errors.GroupBy(f => f.Category);
        var lines = new List<string> { "AiCleverness startup validation failed:" };
        foreach (var group in grouped)
        {
            lines.Add($"  [{group.Key}]");
            foreach (var finding in group)
                lines.Add($"    - {finding.Message}  {finding.Suggestion}");
        }
        throw new InvalidOperationException(string.Join(Environment.NewLine, lines));
    }
}
