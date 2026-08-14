using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Long-running hosted service that wraps <see cref="IAgentRuntime"/> and coordinates
/// graceful shutdown via <see cref="IShutdownCoordinator"/>. It exposes a fire-and-forget
/// <see cref="SubmitAsync"/> method for enqueueing executions while the host is running.
/// </summary>
/// <remarks>
/// <para>
/// This service is designed for scenarios where the agent runtime runs inside an
/// ASP.NET Core or generic host application. It does not start any executions on its own;
/// callers use <see cref="SubmitAsync"/> to kick off work.
/// </para>
/// <para>
/// On <see cref="StopAsync"/>, the service initiates a graceful shutdown through
/// the <see cref="IShutdownCoordinator"/>, giving running executions time to complete
/// or checkpoint their state.
/// </para>
/// </remarks>
public sealed class HostedAgentRuntimeService : IHostedService
{
    private readonly IShutdownCoordinator _coordinator;

    private readonly ILogger<HostedAgentRuntimeService> _logger;

    private readonly HostedRuntimeOptions _options;

    private readonly IAgentRuntime _runtime;

    private int _activeCount;

    /// <summary>
    /// Current number of active (running) executions.
    /// </summary>
    public int ActiveExecutionCount => Volatile.Read(ref _activeCount);

    /// <summary>
    /// Creates a new hosted runtime service.
    /// </summary>
    public HostedAgentRuntimeService(
        IAgentRuntime runtime,
        IShutdownCoordinator coordinator,
        HostedRuntimeOptions options,
        ILogger<HostedAgentRuntimeService> logger)
    {
        _runtime = runtime;
        _coordinator = coordinator;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Hosted agent runtime started. MaxConcurrent={Max}",
            _options.MaxConcurrentExecutions);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Hosted agent runtime stopping. Active executions: {Count}",
            ActiveExecutionCount);

        var completed = await _coordinator.ShutdownAsync(
                            "Host shutdown",
                            _options.ShutdownGracePeriod,
                            cancellationToken).ConfigureAwait(false);

        if (!completed)
        {
            _logger.LogWarning(
                "Graceful shutdown did not complete within {GracePeriod}. {Count} executions may have been interrupted.",
                _options.ShutdownGracePeriod,
                ActiveExecutionCount);
        }
    }

    /// <summary>
    /// Submits an execution request and returns the result.
    /// The execution is tracked by the shutdown coordinator for graceful stop.
    /// </summary>
    /// <param name="request">The agent request to execute.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">External cancellation token.</param>
    /// <returns>The execution result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the concurrency limit is reached.</exception>
    public async Task<AgentResult> SubmitAsync(
        AgentRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_coordinator.IsShuttingDown)
            throw new InvalidOperationException(
                "Runtime is shutting down; cannot accept new executions.");

        if (_options.MaxConcurrentExecutions > 0
            && ActiveExecutionCount >= _options.MaxConcurrentExecutions)
            throw new InvalidOperationException(
                $"Concurrency limit reached ({_options.MaxConcurrentExecutions}). Try again later.");

        var ids = ExecutionIds.Create();
        var shutdownToken = _coordinator.RegisterExecution(ids.ExecutionId);

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken);

        Interlocked.Increment(ref _activeCount);
        _logger.LogDebug(
            "Execution {ExecutionId} started. Active: {Count}",
            ids.ExecutionId,
            ActiveExecutionCount);

        try
        {
            return await _runtime.RunAsync(request, progress, linkedCts.Token)
                       .ConfigureAwait(false);
        }
        finally
        {
            _coordinator.UnregisterExecution(ids.ExecutionId);
            Interlocked.Decrement(ref _activeCount);
            _logger.LogDebug(
                "Execution {ExecutionId} finished. Active: {Count}",
                ids.ExecutionId,
                ActiveExecutionCount);
        }
    }
}
