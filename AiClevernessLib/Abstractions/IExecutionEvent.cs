namespace AiCleverness.Abstractions;

/// <summary>
/// Base interface for all publishable execution events.
/// Events flow through <see cref="IExecutionEventPublisher"/> to registered
/// <see cref="IExecutionEventHandler{TEvent}"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This interface is separate from <c>Models.ExecutionEvent</c>, which is a data record
/// used for journaling. <see cref="IExecutionEvent"/> is the publishable contract used
/// by the in-memory event bus. The runtime converts between the two as needed.
/// </para>
/// <para>
/// All execution events carry an <see cref="ExecutionId"/> so handlers can filter,
/// correlate, and aggregate events for a specific execution.
/// </para>
/// </remarks>
public interface IExecutionEvent
{
    /// <summary>
    /// A discriminator identifying the event type (e.g., "ExecutionStarted", "ToolCompleted").
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// The execution identifier this event belongs to.
    /// </summary>
    string ExecutionId { get; }

    /// <summary>
    /// UTC timestamp when the event was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}
