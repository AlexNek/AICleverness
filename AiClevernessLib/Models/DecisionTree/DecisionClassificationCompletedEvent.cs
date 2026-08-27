using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Journal record emitted after a classification completes.</summary>
public sealed record DecisionClassificationCompletedEvent(
    string ExecutionId,
    string NodeId,
    string Answer,
    string? Observation,
    string? Confidence,
    int Attempt,
    string? TraceId = null,
    string? CorrelationId = null,
    DateTimeOffset? TimestampOverride = null)
    : ExecutionEvent("DecisionClassificationCompleted", TimestampOverride ?? DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
