namespace AiCleverness.Models;

/// <summary>
/// Result of executing a strategy.
/// </summary>
public sealed record StrategyResult(
    bool Success,
    string? Output = null,
    string? Reasoning = null,
    IReadOnlyList<string>? Artifacts = null)
{
    public IReadOnlyList<string> Artifacts { get; init; } = Artifacts ?? Array.Empty<string>();
}
