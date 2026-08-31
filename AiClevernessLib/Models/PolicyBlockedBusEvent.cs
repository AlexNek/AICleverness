using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a policy blocks execution.</summary>
public sealed record PolicyBlockedBusEvent(string ExecutionId, string PolicyName, string? Reason) : IExecutionEvent
{
    public string EventType => "PolicyBlocked";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
