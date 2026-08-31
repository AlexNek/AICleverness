using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when an LLM call completes.</summary>
public sealed record LlmCallCompletedBusEvent(
    string ExecutionId,
    TimeSpan Duration,
    LlmTokenUsage? Usage,
    bool Success = true,
    int Turn = 0,
    string? Error = null) : IExecutionEvent
{
    public string EventType => "LlmCallCompleted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public LlmProviderFailureMetadata? ProviderFailure { get; init; }
}
