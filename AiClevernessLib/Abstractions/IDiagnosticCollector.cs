using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Collects diagnostic entries explaining runtime decisions.
/// Middleware, observers, and runtime components record diagnostics to explain
/// why specific models, tools, strategies, and planners were chosen.
/// </summary>
public interface IDiagnosticCollector
{
    /// <summary>
    /// Clears diagnostics for a specific execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all recorded diagnostic entries across all executions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DiagnosticEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all diagnostic entries for a given execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DiagnosticReport> GetReportAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a diagnostic entry.
    /// </summary>
    /// <param name="entry">The diagnostic entry to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(DiagnosticEntry entry, CancellationToken cancellationToken = default);
}
