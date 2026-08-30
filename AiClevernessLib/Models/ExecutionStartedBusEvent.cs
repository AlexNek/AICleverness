using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when an execution starts.</summary>
public sealed record ExecutionStartedBusEvent(string ExecutionId, AgentRequest Request) : IExecutionEvent
{
    public string EventType => "ExecutionStarted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
