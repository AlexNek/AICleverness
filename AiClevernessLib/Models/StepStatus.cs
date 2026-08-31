namespace AiCleverness.Models;

/// <summary>Status of a step in a partial-success execution.</summary>
public enum StepStatus
{
    Succeeded,
    Failed,
    Skipped,
    Compensated
}
