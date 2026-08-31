namespace AiCleverness.Models;

/// <summary>Raised when an execution completes (success or failure).</summary>
public sealed record ExecutionCompletedEvent(string ExecutionId, AgentResult Result, TimeSpan Duration, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("ExecutionCompleted", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
