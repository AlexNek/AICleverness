using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Persistence abstraction for storing and retrieving execution snapshots.
/// Implementations might use files, databases, blob storage, or in-memory dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// Checkpoints are immutable once written. Each write produces a new
/// <see cref="CheckpointEntry"/> identified by a unique <c>CheckpointId</c>.
/// Callers can enumerate all checkpoints for an execution or load a specific one.
/// </para>
/// </remarks>
public interface ICheckpointStore
{
    /// <summary>
    /// Deletes all checkpoints for the given execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all checkpoints for a given execution, ordered by capture time descending.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<CheckpointEntry>> ListAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific checkpoint by its identifier.
    /// </summary>
    /// <param name="checkpointId">Checkpoint identifier returned by <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or <c>null</c> if not found.</returns>
    Task<ExecutionSnapshot?> LoadAsync(
        string checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the most recent snapshot for the given execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest snapshot, or <c>null</c> if none exists.</returns>
    Task<ExecutionSnapshot?> LoadLatestAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a snapshot and returns the checkpoint entry describing it.
    /// </summary>
    /// <param name="snapshot">The snapshot to persist.</param>
    /// <param name="label">Optional human-readable label (e.g. "before-tool-call").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint entry describing the stored snapshot.</returns>
    Task<CheckpointEntry> SaveAsync(
        ExecutionSnapshot snapshot,
        string? label = null,
        CancellationToken cancellationToken = default);
}
