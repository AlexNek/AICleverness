namespace AiCleverness.Models;

/// <summary>
/// Raised when an LLM call fails (timeout or error) before producing a response.
/// Failed attempts are journal/manifest events in their own right — they must
/// not be represented as <see cref="LlmRespondedEvent"/>.
/// </summary>
public sealed record LlmFailedEvent(
    string ExecutionId,
    string Error,
    TimeSpan Duration,
    string? TraceId = null,
    string? CorrelationId = null,
    int Turn = 0)
    : ExecutionEvent("LlmFailed", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
