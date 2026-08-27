namespace AiCleverness.Models.DecisionTree;

/// <summary>Classification captured from a classify node.</summary>
public sealed record DecisionClassification(
    string NodeId,
    string Answer,
    string? Observation,
    string? Confidence,
    DateTimeOffset At);
