namespace AiCleverness.Abstractions;

/// <summary>
/// Hook invoked during graceful shutdown to allow running executions
/// to persist state, cancel cleanly, or request a deferred restart.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are discovered via DI and invoked in registration order.
/// Each hook receives the reason for shutdown and a deadline token that fires
/// when the shutdown grace period expires.
/// </para>
/// </remarks>
public interface IShutdownHook
{
    /// <summary>Display name for diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Called when the runtime is shutting down.
    /// </summary>
    /// <param name="reason">Human-readable reason for the shutdown.</param>
    /// <param name="deadline">Token that fires when the shutdown grace period expires.</param>
    /// <returns>A task that completes when the hook has finished its cleanup.</returns>
    Task OnShutdownAsync(string reason, CancellationToken deadline);
}
