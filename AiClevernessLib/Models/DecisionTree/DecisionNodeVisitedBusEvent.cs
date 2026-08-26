using AiCleverness.Abstractions;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Publishable event emitted when a decision node is visited.</summary>
public sealed record DecisionNodeVisitedBusEvent(
    string ExecutionId,
    string NodeId,
    EDecisionNodeType NodeType,
    TimeSpan Duration,
    string? OutcomeJson,
    DateTimeOffset? TimestampOverride = null,
    string? TraceId = null,
    string? CorrelationId = null) : IExecutionEvent
{
    public string EventType => "DecisionNodeVisited";
    public string? TraceId { get; init; } = TraceId;
    public string? CorrelationId { get; init; } = CorrelationId;
    public DateTimeOffset Timestamp { get; init; } = TimestampOverride ?? DateTimeOffset.UtcNow;
}
