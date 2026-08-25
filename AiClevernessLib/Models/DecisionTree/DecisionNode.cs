using System.Text.Json;

namespace AiCleverness.Models.DecisionTree;

/// <summary>A node in a declarative decision tree.</summary>
public sealed record DecisionNode
{
    public required EDecisionNodeType Type { get; init; }
    public IReadOnlyList<DecisionTransition> Transitions { get; init; } = Array.Empty<DecisionTransition>();
    public string? ActionName { get; init; }
    public string? Question { get; init; }
    public IReadOnlyList<string>? Answers { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? PredicateParameters { get; init; }
    public string? PredicateName { get; init; }
    public string? Verdict { get; init; }
}
