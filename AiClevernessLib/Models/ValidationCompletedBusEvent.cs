using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a result validator evaluates a result.</summary>
public sealed record ValidationCompletedBusEvent(string ExecutionId, string ValidatorName, bool IsValid, string? Error) : IExecutionEvent
{
    public string EventType => "ValidationCompleted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
