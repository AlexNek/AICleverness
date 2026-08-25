namespace AiCleverness.Models.DecisionTree;

/// <summary>Result returned by a decision action.</summary>
public sealed record DecisionActionResult(
    IReadOnlyList<DecisionData>? ProducedData,
    IReadOnlyDictionary<string, string>? Properties,
    DecisionActionStatus Status,
    string? Error = null);
