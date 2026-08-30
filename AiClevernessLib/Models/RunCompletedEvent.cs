namespace AiCleverness.Models;

/// <summary>Raised when the execution completes (success or failure).</summary>
public sealed record RunCompletedEvent : AgentEvent
{
    /// <summary>Total execution duration.</summary>
    public required TimeSpan Duration { get; init; }

    public override string EventType => "RunCompleted";

    /// <summary>The final result.</summary>
    public required AgentResult Result { get; init; }
}
