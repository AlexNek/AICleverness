namespace AiCleverness.Models;

/// <summary>
/// Base class for all streaming agent events.
/// Events are emitted during execution and represent observable state transitions.
/// </summary>
public abstract record AgentEvent
{
    /// <summary>Discriminator identifying the event type.</summary>
    public abstract string EventType { get; }

    /// <summary>Execution identifier this event belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>UTC timestamp when the event was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
