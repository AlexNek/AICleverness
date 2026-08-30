namespace AiCleverness.Abstractions;

/// <summary>
/// Coordinates graceful shutdown of running executions.
/// The coordinator tracks active executions and signals them to stop when
/// shutdown is initiated.
/// </summary>
public interface IShutdownCoordinator
{
    /// <summary>Number of currently registered (active) executions.</summary>
    int ActiveExecutionCount { get; }

    /// <summary>Whether shutdown has been requested.</summary>
    bool IsShuttingDown { get; }

    /// <summary>
    /// Registers a running execution for tracking during shutdown.
    /// Returns a <see cref="CancellationToken"/> that will be cancelled when shutdown is requested.
    /// </summary>
    /// <param name="executionId">Execution identifier being tracked.</param>
    /// <returns>A token that fires when the execution should stop.</returns>
    CancellationToken RegisterExecution(string executionId);

    /// <summary>
    /// Initiates graceful shutdown, signalling all registered executions to stop.
    /// </summary>
    /// <param name="reason">Human-readable reason for shutdown.</param>
    /// <param name="gracePeriod">Maximum time to wait for executions to complete.</param>
    /// <param name="cancellationToken">External cancellation token.</param>
    /// <returns><c>true</c> if all executions completed within the grace period; otherwise <c>false</c>.</returns>
    Task<bool> ShutdownAsync(
        string reason,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a completed execution from tracking.
    /// </summary>
    /// <param name="executionId">Execution identifier that has completed.</param>
    void UnregisterExecution(string executionId);
}
