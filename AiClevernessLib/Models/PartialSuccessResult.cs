namespace AiCleverness.Models;

/// <summary>
/// Result model representing partial success when some steps succeeded and others failed.
/// Extends <see cref="AgentResult"/> with detailed per-step status.
/// </summary>
public sealed record PartialSuccessResult
{
    /// <summary>Steps that failed.</summary>
    public IReadOnlyList<StepOutcome> FailedSteps { get; init; } = Array.Empty<StepOutcome>();

    /// <summary>Whether this is considered a partial success (some steps succeeded, some failed).</summary>
    public bool IsPartialSuccess => SucceededSteps.Count > 0 && FailedSteps.Count > 0;

    /// <summary>The overall agent result.</summary>
    public required AgentResult Result { get; init; }

    /// <summary>Steps that were skipped due to earlier failures.</summary>
    public IReadOnlyList<StepOutcome> SkippedSteps { get; init; } = Array.Empty<StepOutcome>();

    /// <summary>Steps that completed successfully.</summary>
    public IReadOnlyList<StepOutcome> SucceededSteps { get; init; } = Array.Empty<StepOutcome>();

    /// <summary>Overall success ratio (0.0 to 1.0).</summary>
    public double SuccessRatio =>
        TotalSteps == 0
            ? 0.0
            : (double)SucceededSteps.Count / TotalSteps;

    /// <summary>Total number of steps.</summary>
    public int TotalSteps => SucceededSteps.Count + FailedSteps.Count + SkippedSteps.Count;
}

/// <summary>
/// Outcome of a single step within a partial-success execution.
/// </summary>
public sealed record StepOutcome(
    string StepName,
    StepStatus Status,
    string? Output = null,
    string? Error = null,
    TimeSpan? Duration = null);

/// <summary>
/// Status of a step in a partial-success execution.
/// </summary>
public enum StepStatus
{
    /// <summary>Step completed successfully.</summary>
    Succeeded,

    /// <summary>Step failed.</summary>
    Failed,

    /// <summary>Step was skipped.</summary>
    Skipped,

    /// <summary>Step was compensated (rolled back).</summary>
    Compensated
}
