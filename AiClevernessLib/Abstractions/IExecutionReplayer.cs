using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Replays a previously recorded execution using stored snapshots and journal entries.
/// </summary>
/// <remarks>
/// <para>
/// Replay creates a new execution with a fresh <see cref="ExecutionIds"/> but reuses
/// the original request parameters (unless overridden). The replayer depends on
/// <see cref="ICheckpointStore"/> and optionally <see cref="IExecutionJournal"/> to
/// reconstruct execution state.
/// </para>
/// </remarks>
public interface IExecutionReplayer
{
    /// <summary>
    /// Checks whether the given execution has enough persisted state to be replayed.
    /// </summary>
    /// <param name="executionId">Execution identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if replay is possible; otherwise <c>false</c>.</returns>
    Task<bool> CanReplayAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays a previously recorded execution.
    /// </summary>
    /// <param name="request">Replay parameters specifying which execution to replay.</param>
    /// <param name="progress">Optional progress reporter for replay status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the replay.</returns>
    Task<ReplayResult> ReplayAsync(
        ReplayRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
