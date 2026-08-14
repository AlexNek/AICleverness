namespace AiCleverness.Models;

/// <summary>
/// Declarative definition of a tool exposed to an agent.
/// </summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    string? ParametersSchema = null,
    string? Category = null,
    Version? Version = null,
    decimal? CostPerCall = null,
    bool RequiresApproval = false,
    TimeSpan? DefaultTimeout = null,
    bool Parallelizable = false,
    string? DangerLevel = null,
    string? Authentication = null,
    IReadOnlyList<string>? Tags = null)
{
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? Array.Empty<string>();
}
