namespace AiCleverness.Models;

/// <summary>
/// Immutable snapshot of what an execution consisted of.
/// Useful for replay, auditing, and debugging.
/// </summary>
public sealed record ExecutionManifest(
    string ExecutionId,
    string? TraceId,
    string? CorrelationId,
    ExecutionStatus Status,
    DateTimeOffset CreatedAt,
    TimeSpan? Duration,
    AgentRequest Request,
    AgentRuntimeOptions Options,
    IReadOnlyList<string> ToolNames,
    int TurnCount,
    int QualityRetryCount,
    int ToolRetryCount,
    IReadOnlyList<ExecutionEvent>? Events = null)
{
    public IReadOnlyList<ExecutionEvent> Events { get; init; } =
        Events ?? Array.Empty<ExecutionEvent>();
}
