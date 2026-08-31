using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a tool invocation completes.</summary>
public sealed record ToolCompletedBusEvent(string ExecutionId, string ToolName, ToolResult Result, TimeSpan Duration) : IExecutionEvent
{
    public string EventType => "ToolCompleted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
