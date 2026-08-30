namespace AiCleverness.Models;

/// <summary>Raised when an LLM responds.</summary>
public sealed record LlmRespondedEvent(string ExecutionId, LlmResponse Response, TimeSpan Duration, string? TraceId = null, string? CorrelationId = null, int Turn = 0)
    : ExecutionEvent("LlmResponded", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
