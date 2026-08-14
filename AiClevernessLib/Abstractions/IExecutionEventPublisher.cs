namespace AiCleverness.Abstractions;

/// <summary>
/// Publishes execution events to the internal event bus.
/// Handlers registered for the event type are invoked asynchronously.
/// </summary>
/// <remarks>
/// <para>
/// The publisher is fire-and-forget from the runtime's perspective. Handler failures
/// are logged but do not affect the execution. This ensures that event handling is
/// purely observational — it cannot change execution behavior.
/// </para>
/// <para>
/// Events are dispatched in registration order. All handlers for a given event type
/// are invoked before the publisher returns.
/// </para>
/// </remarks>
public interface IExecutionEventPublisher
{
    /// <summary>
    /// Publishes an event to all registered handlers.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when all handlers have been invoked.</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IExecutionEvent;
}
