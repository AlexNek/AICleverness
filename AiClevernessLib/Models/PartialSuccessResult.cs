namespace AiCleverness.Models;

/// <summary>Result model representing partial success when some steps succeeded and others failed.</summary>
public sealed record PartialSuccessResult
{
    public IReadOnlyList<StepOutcome> FailedSteps { get; init; } = Array.Empty<StepOutcome>();
    public bool IsPartialSuccess => SucceededSteps.Count > 0 && FailedSteps.Count > 0;
    public required AgentResult Result { get; init; }
    public IReadOnlyList<StepOutcome> SkippedSteps { get; init; } = Array.Empty<StepOutcome>();
    public IReadOnlyList<StepOutcome> SucceededSteps { get; init; } = Array.Empty<StepOutcome>();
    public double SuccessRatio => TotalSteps == 0 ? 0.0 : (double)SucceededSteps.Count / TotalSteps;
    public int TotalSteps => SucceededSteps.Count + FailedSteps.Count + SkippedSteps.Count;
}
