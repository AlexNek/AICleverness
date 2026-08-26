using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingExecutionEventPublisher : IExecutionEventPublisher
{
    public List<IExecutionEvent> Events { get; } = [];

    public Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IExecutionEvent
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }
}