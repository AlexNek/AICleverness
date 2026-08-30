namespace AiCleverness.Models;

/// <summary>
/// Base record for structured execution events.
/// Subtypes carry event-specific data for batch observation and journaling.
/// </summary>
public abstract record ExecutionEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string ExecutionId,
    string? TraceId = null,
    string? CorrelationId = null);
