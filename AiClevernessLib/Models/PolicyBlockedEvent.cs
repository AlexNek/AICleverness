namespace AiCleverness.Models;

/// <summary>Raised when a policy blocks execution.</summary>
public sealed record PolicyBlockedEvent(string ExecutionId, string PolicyName, PolicyResult PolicyResult, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("PolicyBlocked", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
