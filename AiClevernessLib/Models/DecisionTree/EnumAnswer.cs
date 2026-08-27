namespace AiCleverness.Models.DecisionTree;

/// <summary>Bounded answer parsed from a classify-node response.</summary>
public sealed record EnumAnswer(string Value, string? Observation, string? Confidence);
