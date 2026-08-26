namespace AiCleverness.Models.DecisionTree;

/// <summary>Bounded answer parsed from a question-node response.</summary>
public sealed record EnumAnswer(string Value, string? Observation, string? Confidence);
