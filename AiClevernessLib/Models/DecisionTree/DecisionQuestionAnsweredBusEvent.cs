using AiCleverness.Abstractions;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Publishable event emitted after a question answer is classified.</summary>
public sealed record DecisionQuestionAnsweredBusEvent(
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
    public string EventType => "DecisionQuestionAnswered";
    public string? TraceId { get; init; } = TraceId;
    public string? CorrelationId { get; init; } = CorrelationId;
    public DateTimeOffset Timestamp { get; init; } = TimestampOverride ?? DateTimeOffset.UtcNow;
}
