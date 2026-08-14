using AiCleverness.Abstractions;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory implementation of <see cref="IExecutionEventPublisher"/>.
/// Resolves all registered <see cref="IExecutionEventHandler{TEvent}"/> instances from DI
/// and invokes them in registration order.
/// </summary>
/// <remarks>
/// <para>
/// Handler failures are caught and logged — they do not propagate to the publisher or
/// affect the ongoing execution. This ensures event handling is purely observational.
/// </para>
/// <para>
/// This implementation is thread-safe. Concurrent publishes are handled independently.
/// </para>
/// </remarks>
public sealed class InMemoryEventBus : IExecutionEventPublisher
{
    private readonly ILogger<InMemoryEventBus>? _logger;

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of the in-memory event bus.
    /// </summary>
    /// <param name="serviceProvider">DI service provider for resolving handlers.</param>
    /// <param name="logger">Optional logger.</param>
    public InMemoryEventBus(
        IServiceProvider serviceProvider,
        ILogger<InMemoryEventBus>? logger = null)
    {
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IExecutionEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handlers =
            _serviceProvider.GetService(typeof(IEnumerable<IExecutionEventHandler<TEvent>>))
                as IEnumerable<IExecutionEventHandler<TEvent>>;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(
                    ex,
                    "Event handler {HandlerType} failed for event {EventType} on execution {ExecutionId}",
                    handler.GetType().Name,
                    @event.EventType,
                    @event.ExecutionId);
            }
        }
    }
}
