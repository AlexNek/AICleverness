namespace AiCleverness.Models;

/// <summary>Outcome of an execution replay.</summary>
public sealed record ReplayResult
{
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public required string OriginalExecutionId { get; init; }
    public required string ReplayExecutionId { get; init; }
    public AgentResult? Result { get; init; }
    public string? ResumedFromCheckpointId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public bool Success { get; init; }
}
