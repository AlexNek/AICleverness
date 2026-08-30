using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Journal record emitted after an action node completes.</summary>
public sealed record DecisionActionCompletedEvent(
    string ExecutionId,
    string NodeId,
    string ActionKey,
    DecisionActionStatus Status,
    string? Error = null,
    string? TraceId = null,
    string? CorrelationId = null,
    DateTimeOffset? TimestampOverride = null)
    : ExecutionEvent("DecisionActionCompleted", TimestampOverride ?? DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
