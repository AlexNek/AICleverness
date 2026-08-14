namespace AiCleverness.Models;

/// <summary>
/// A single entry in the execution journal, wrapping an execution event
/// with a monotonically increasing sequence number for ordered replay.
/// </summary>
public sealed record JournalEntry(
    long SequenceNumber,
    string ExecutionId,
    string EventType,
    DateTimeOffset Timestamp,
    string? SerializedPayload = null,
    string? TraceId = null,
    string? CorrelationId = null)
{
    /// <summary>Creates a journal entry from an execution event.</summary>
    public static JournalEntry From(
        ExecutionEvent evt,
        long sequenceNumber,
        string? serializedPayload = null) =>
        new(
            sequenceNumber,
            evt.ExecutionId,
            evt.EventType,
            evt.Timestamp,
            serializedPayload,
            evt.TraceId,
            evt.CorrelationId);
}
