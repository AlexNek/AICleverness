namespace AiCleverness.Models;

/// <summary>Result of a scheduled execution attempt.</summary>
public sealed record ScheduledExecutionResult
{
    public DateTimeOffset CompletedAt { get; init; }
    public string? Error { get; init; }
    public required string ExecutionId { get; init; }
    public AgentResult? Result { get; init; }
    public required string ScheduleId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public bool Success { get; init; }
}
