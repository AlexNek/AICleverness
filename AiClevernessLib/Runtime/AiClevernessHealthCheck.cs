using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiCleverness.Runtime;

/// <summary>
/// ASP.NET Core health check adapter for the AiCleverness runtime.
/// Reports the runtime as unhealthy during shutdown or when too many executions are active.
/// </summary>
public sealed class AiClevernessHealthCheck : IHealthCheck
{
    private readonly IShutdownCoordinator? _coordinator;

    private readonly HostedRuntimeOptions? _options;

    /// <summary>
    /// Creates a health check with optional coordinator and options.
    /// </summary>
    public AiClevernessHealthCheck(
        IShutdownCoordinator? coordinator = null,
        HostedRuntimeOptions? options = null)
    {
        _coordinator = coordinator;
        _options = options;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var isShuttingDown = _coordinator?.IsShuttingDown ?? false;
        var activeCount = _coordinator?.ActiveExecutionCount ?? 0;

        data["ActiveExecutions"] = activeCount;
        data["IsShuttingDown"] = isShuttingDown;

        if (isShuttingDown)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Runtime is shutting down.",
                    data: data));
        }

        if (_options is { MaxConcurrentExecutions: > 0 } &&
            activeCount >= _options.MaxConcurrentExecutions)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"Concurrency limit reached ({_options.MaxConcurrentExecutions}).",
                    data: data));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy(
                "Runtime is operational.",
                data: data));
    }
}
