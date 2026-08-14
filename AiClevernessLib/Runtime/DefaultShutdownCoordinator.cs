using System.Collections.Concurrent;

using AiCleverness.Abstractions;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Default <see cref="IShutdownCoordinator"/> that tracks active executions via
/// <see cref="CancellationTokenSource"/> and invokes registered <see cref="IShutdownHook"/>s on shutdown.
/// </summary>
public sealed class DefaultShutdownCoordinator : IShutdownCoordinator
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new();

    private readonly CancellationTokenSource _globalCts = new();

    private readonly IEnumerable<IShutdownHook> _hooks;

    private readonly ILogger<DefaultShutdownCoordinator> _logger;

    private int _isShuttingDown;

    /// <inheritdoc />
    public int ActiveExecutionCount => _active.Count;

    /// <inheritdoc />
    public bool IsShuttingDown => Volatile.Read(ref _isShuttingDown) == 1;

    /// <summary>
    /// Creates a new coordinator with the given hooks and logger.
    /// </summary>
    public DefaultShutdownCoordinator(
        IEnumerable<IShutdownHook> hooks,
        ILogger<DefaultShutdownCoordinator> logger)
    {
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public CancellationToken RegisterExecution(string executionId)
    {
        if (IsShuttingDown)
            throw new InvalidOperationException(
                "Cannot register executions while shutdown is in progress.");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        if (!_active.TryAdd(executionId, cts))
        {
            cts.Dispose();
            throw new InvalidOperationException(
                $"Execution '{executionId}' is already registered.");
        }

        return cts.Token;
    }

    /// <inheritdoc />
    public async Task<bool> ShutdownAsync(
        string reason,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isShuttingDown, 1) == 1)
        {
            _logger.LogWarning("Shutdown already in progress");
            return false;
        }

        _logger.LogInformation(
            "Graceful shutdown initiated: {Reason}. Active executions: {Count}",
            reason,
            _active.Count);

        // Signal all executions to stop
        try
        {
            await _globalCts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, continue
        }

        // Create a deadline from the grace period
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(gracePeriod);
        var deadline = deadlineCts.Token;

        // Run shutdown hooks in registration order
        foreach (var hook in _hooks)
        {
            try
            {
                _logger.LogDebug("Invoking shutdown hook: {HookName}", hook.Name);
                await hook.OnShutdownAsync(reason, deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                _logger.LogWarning("Shutdown hook '{HookName}' exceeded grace period", hook.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shutdown hook '{HookName}' failed", hook.Name);
            }
        }

        // Wait for active executions to complete
        if (_active.Count > 0)
        {
            try
            {
                await Task.Delay(gracePeriod, deadline).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Grace period expired with {Count} executions still active",
                    _active.Count);
                return false;
            }
        }

        _logger.LogInformation(
            "Graceful shutdown completed. Remaining active executions: {Count}",
            _active.Count);
        return _active.Count == 0;
    }

    /// <inheritdoc />
    public void UnregisterExecution(string executionId)
    {
        if (_active.TryRemove(executionId, out var cts))
        {
            cts.Dispose();
        }
    }
}
