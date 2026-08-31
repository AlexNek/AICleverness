namespace AiCleverness.Models;

/// <summary>Raised when execution is cancelled.</summary>
public sealed record CancellationEvent : AgentEvent
{
    public override string EventType => "Cancelled";

    /// <summary>Optional reason for cancellation.</summary>
    public string? Reason { get; init; }
}
