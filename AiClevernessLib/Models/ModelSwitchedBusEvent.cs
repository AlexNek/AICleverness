using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>
/// Publishable event raised when the runtime switches to a fallback model.
/// </summary>
public sealed record ModelSwitchedBusEvent(
    string ExecutionId,
    string From,
    string To,
    string Reason,
    int Turn) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ModelSwitched";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
