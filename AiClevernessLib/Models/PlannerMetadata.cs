namespace AiCleverness.Models;

/// <summary>
/// Descriptive metadata about a planner implementation.
/// Used by the planner registry for discovery and selection.
/// </summary>
public sealed record PlannerMetadata
{
    /// <summary>Human-readable description of what this planner does.</summary>
    public string? Description { get; init; }

    /// <summary>Unique name identifying this planner.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this planner requires an LLM call.
    /// False for deterministic/rule-based planners.
    /// </summary>
    public bool RequiresLlm { get; init; }

    /// <summary>Optional tags for categorization (e.g. "sequential", "parallel", "hierarchical").</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
