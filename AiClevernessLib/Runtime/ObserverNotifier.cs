using AiCleverness.Abstractions;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Shared helper that notifies all agent observers, isolating individual observer
/// failures so a single failing observer cannot break an execution.
/// </summary>
internal static class ObserverNotifier
{
    /// <summary>
    /// Invokes <paramref name="notify"/> for every observer. Observer exceptions are
    /// logged and swallowed, except cancellation when <paramref name="cancellationToken"/>
    /// itself was cancelled, which is re-thrown.
    /// </summary>
    public static async Task NotifyAllAsync(
        IEnumerable<IAgentObserver> observers,
        Func<IAgentObserver, Task> notify,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await notify(observer);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Agent observer {ObserverType} failed",
                    observer.GetType().Name);
            }
        }
    }
}
