using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when an execution completes (success or failure).</summary>
public sealed record ExecutionCompletedBusEvent(string ExecutionId, AgentResult Result, TimeSpan? Duration) : IExecutionEvent
{
    public string EventType => "ExecutionCompleted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
