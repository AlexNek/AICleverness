namespace AiCleverness.Models;

/// <summary>Outcome of a single step within a partial-success execution.</summary>
public sealed record StepOutcome(
    string StepName,
    StepStatus Status,
    string? Output = null,
    string? Error = null,
    TimeSpan? Duration = null);
