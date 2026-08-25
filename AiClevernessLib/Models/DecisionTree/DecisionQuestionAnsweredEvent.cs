using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Journal record emitted after a question answer is classified.</summary>
public sealed record DecisionQuestionAnsweredEvent(
    string ExecutionId,
    string NodeId,
    string Answer,
    string? Observation,
    string? Confidence,
    int Attempt,
    string? TraceId = null,
    string? CorrelationId = null,
    DateTimeOffset? TimestampOverride = null)
    : ExecutionEvent("DecisionQuestionAnswered", TimestampOverride ?? DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
