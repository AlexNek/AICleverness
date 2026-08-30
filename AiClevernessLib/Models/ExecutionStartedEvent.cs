namespace AiCleverness.Models;

/// <summary>Raised when an execution starts.</summary>
public sealed record ExecutionStartedEvent(string ExecutionId, AgentRequest Request, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("ExecutionStarted", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
