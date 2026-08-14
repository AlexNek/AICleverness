using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Collects and retrieves execution metrics for observability.
/// </summary>
/// <remarks>
/// Implementations may store metrics in memory, forward to a metrics pipeline,
/// or aggregate them across multiple executions.
/// </remarks>
public interface IMetricsCollector
{
    /// <summary>
    /// Gets aggregated metrics across all recorded executions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExecutionMetrics> GetAggregateMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for a specific execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExecutionMetrics?> GetExecutionMetricsAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets per-tool metrics breakdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ToolMetrics>> GetToolMetricsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records metrics from a completed execution.
    /// </summary>
    /// <param name="manifest">The execution manifest to extract metrics from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(ExecutionManifest manifest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all collected metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetAsync(CancellationToken cancellationToken = default);
}
