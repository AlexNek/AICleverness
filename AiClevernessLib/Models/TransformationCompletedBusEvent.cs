using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a result transformer modifies a result.</summary>
public sealed record TransformationCompletedBusEvent(string ExecutionId, string TransformerName) : IExecutionEvent
{
    public string EventType => "TransformationCompleted";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
