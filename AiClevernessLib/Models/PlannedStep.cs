namespace AiCleverness.Models;

/// <summary>
/// A single step in an agent-generated plan.
/// </summary>
public sealed record PlannedStep(
    string Name,
    string Type,
    string? Description = null,
    IReadOnlyDictionary<string, object>? Parameters = null)
{
    public IReadOnlyDictionary<string, object> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, object>();
}
