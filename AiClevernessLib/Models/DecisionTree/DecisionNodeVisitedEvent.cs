using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Journal record emitted when a decision node is visited.</summary>
public sealed record DecisionNodeVisitedEvent(
    string ExecutionId,
    string NodeId,
    EDecisionNodeType NodeType,
    TimeSpan Duration,
    string? OutcomeJson,
    string? TraceId = null,
    string? CorrelationId = null,
    DateTimeOffset? TimestampOverride = null)
    : ExecutionEvent("DecisionNodeVisited", TimestampOverride ?? DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
