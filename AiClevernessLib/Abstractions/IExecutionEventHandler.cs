namespace AiCleverness.Abstractions;

/// <summary>
/// Handles execution events of a specific type.
/// Implement this interface and register via DI to receive events from the event bus.
/// </summary>
/// <typeparam name="TEvent">The event type to handle.</typeparam>
/// <remarks>
/// <para>
/// Handlers are invoked in registration order for each published event of the matching type.
/// Handler failures are caught by the event bus and logged — they do not affect execution.
/// </para>
/// <para>
/// Multiple handlers for the same event type are supported. A handler can implement
/// <see cref="IExecutionEventHandler{TEvent}"/> for multiple event types by implementing
/// the interface multiple times (or by using separate handler classes).
/// </para>
/// </remarks>
public interface IExecutionEventHandler<in TEvent>
    where TEvent : IExecutionEvent
{
    /// <summary>
    /// Handles the given execution event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
