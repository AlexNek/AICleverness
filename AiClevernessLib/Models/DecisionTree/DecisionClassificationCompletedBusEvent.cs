using AiCleverness.Abstractions;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Publishable event emitted after a classification completes.</summary>
public sealed record DecisionClassificationCompletedBusEvent(
    string ExecutionId,
    string NodeId,
    string Answer,
    string? Observation,
    string? Confidence,
    int Attempt,
    DateTimeOffset? TimestampOverride = null,
    string? TraceId = null,
    string? CorrelationId = null) : IExecutionEvent
{
    public string EventType => "DecisionClassificationCompleted";
    public string? TraceId { get; init; } = TraceId;
    public string? CorrelationId { get; init; } = CorrelationId;
    public DateTimeOffset Timestamp { get; init; } = TimestampOverride ?? DateTimeOffset.UtcNow;
}
