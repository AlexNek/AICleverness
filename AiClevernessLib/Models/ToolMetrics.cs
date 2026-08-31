namespace AiCleverness.Models;

/// <summary>Per-tool breakdown metrics.</summary>
public sealed record ToolMetrics(
    string ToolName,
    long InvocationCount,
    long FailureCount,
    TimeSpan? AverageDuration,
    TimeSpan? MaxDuration)
{
    public double? FailureRate => InvocationCount > 0 ? (double)FailureCount / InvocationCount : null;
}
