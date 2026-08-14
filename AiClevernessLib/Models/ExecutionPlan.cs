namespace AiCleverness.Models;

/// <summary>
/// Structured execution plan produced by a planner.
/// Wraps the list of steps with metadata about how the plan was created.
/// </summary>
public sealed record ExecutionPlan
{
    /// <summary>UTC timestamp when the plan was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>The original goal this plan addresses.</summary>
    public string? Goal { get; init; }

    /// <summary>Additional metadata about plan creation.</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Unique identifier for this plan.</summary>
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Name of the planner that produced this plan.</summary>
    public required string PlannerName { get; init; }

    /// <summary>Optional reasoning or explanation of the plan.</summary>
    public string? Reasoning { get; init; }

    /// <summary>The ordered steps in this plan.</summary>
    public required IReadOnlyList<PlannedStep> Steps { get; init; }

    /// <summary>Creates an empty plan (no steps).</summary>
    public static ExecutionPlan Empty(string plannerName) =>
        new() { PlannerName = plannerName, Steps = Array.Empty<PlannedStep>() };
}
