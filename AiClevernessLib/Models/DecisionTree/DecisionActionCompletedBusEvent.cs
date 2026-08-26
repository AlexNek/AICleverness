using AiCleverness.Abstractions;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Publishable event emitted after an action node completes.</summary>
public sealed record DecisionActionCompletedBusEvent(
    string ExecutionId,
    string NodeId,
    string ActionName,
    DecisionActionStatus Status,
    string? Error = null,
    DateTimeOffset? TimestampOverride = null,
    string? TraceId = null,
    string? CorrelationId = null) : IExecutionEvent
{
    public string EventType => "DecisionActionCompleted";
    public string? TraceId { get; init; } = TraceId;
    public string? CorrelationId { get; init; } = CorrelationId;
    public DateTimeOffset Timestamp { get; init; } = TimestampOverride ?? DateTimeOffset.UtcNow;
}
