namespace AiCleverness.Models;

/// <summary>
/// Describes a request to replay a previously recorded execution.
/// </summary>
public sealed record ReplayRequest
{
    /// <summary>Execution identifier of the original run to replay.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Optional checkpoint identifier to resume from a specific point.
    /// When <c>null</c>, replay starts from the beginning.
    /// </summary>
    public string? FromCheckpointId { get; init; }

    /// <summary>
    /// Optional override goal for the replay. If null, the original goal is reused.
    /// </summary>
    public string? OverrideGoal { get; init; }

    /// <summary>
    /// Optional override tool names. If null, the original tool set is reused.
    /// </summary>
    public IReadOnlyList<string>? OverrideToolNames { get; init; }

    /// <summary>
    /// When <c>true</c>, the replay uses the same request parameters
    /// as the original execution. When <c>false</c>, callers may override them.
    /// </summary>
    public bool UseOriginalParameters { get; init; } = true;
}

/// <summary>
/// Outcome of an execution replay.
/// </summary>
public sealed record ReplayResult
{
    /// <summary>UTC timestamp when the replay completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Total replay duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Execution identifier of the original run that was replayed.</summary>
    public required string OriginalExecutionId { get; init; }

    /// <summary>Execution identifier of the replayed run (new execution).</summary>
    public required string ReplayExecutionId { get; init; }

    /// <summary>The result of the replayed execution.</summary>
    public AgentResult? Result { get; init; }

    /// <summary>
    /// The checkpoint identifier the replay resumed from, if any.
    /// Null indicates the replay started from the beginning.
    /// </summary>
    public string? ResumedFromCheckpointId { get; init; }

    /// <summary>UTC timestamp when the replay started.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Whether the replay completed successfully.</summary>
    public bool Success { get; init; }
}
