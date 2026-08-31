using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a tool invocation fails with an exception.</summary>
public sealed record ToolFailedBusEvent(string ExecutionId, string ToolName, string ErrorMessage) : IExecutionEvent
{
    public string EventType => "ToolFailed";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
