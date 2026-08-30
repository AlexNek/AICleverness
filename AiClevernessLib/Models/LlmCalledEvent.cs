namespace AiCleverness.Models;

/// <summary>Raised when an LLM is called.</summary>
public sealed record LlmCalledEvent(string ExecutionId, IReadOnlyList<LlmMessage> Messages, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("LlmCalled", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
