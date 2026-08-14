using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Append-only log for recording execution events.
/// The journal provides ordered, durable event storage for replay, auditing, and diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// Each entry receives a monotonically increasing sequence number within an execution.
/// Implementations must guarantee that sequence numbers are unique and ordered per execution.
/// </para>
/// <para>
/// The journal is intentionally provider-neutral: it stores serialized payloads
/// (strings) rather than concrete event types, so it can be implemented with any
/// storage backend (files, databases, event stores).
/// </para>
/// </remarks>
public interface IExecutionJournal
{
    /// <summary>
    /// Appends an event to the journal for the given execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="evt">The execution event to record.</param>
    /// <param name="serializedPayload">Optional serialized event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The journal entry with assigned sequence number.</returns>
    Task<JournalEntry> AppendAsync(
        string executionId,
        ExecutionEvent evt,
        string? serializedPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all journal entries for the given execution.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest sequence number for the given execution, or -1 if empty.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> GetLatestSequenceAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads journal entries after the given sequence number, ordered ascending.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="afterSequence">Exclusive lower bound sequence number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<JournalEntry>> ReadAfterAsync(
        string executionId,
        long afterSequence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all journal entries for the given execution, ordered by sequence number.
    /// </summary>
    /// <param name="executionId">Execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<JournalEntry>> ReadAllAsync(
        string executionId,
        CancellationToken cancellationToken = default);
}
