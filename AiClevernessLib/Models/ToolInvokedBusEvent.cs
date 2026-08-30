using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a tool invocation begins.</summary>
public sealed record ToolInvokedBusEvent(string ExecutionId, string ToolName, ToolInvocation Invocation) : IExecutionEvent
{
    public string EventType => "ToolInvoked";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
