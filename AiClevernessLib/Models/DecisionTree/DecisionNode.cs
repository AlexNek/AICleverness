using System.Text.Json;

namespace AiCleverness.Models.DecisionTree;

/// <summary>A node in a declarative decision tree.</summary>
public sealed record DecisionNode
{
    public required EDecisionNodeType Type { get; init; }
    public IReadOnlyList<DecisionTransition> Transitions { get; init; } = Array.Empty<DecisionTransition>();
    public string? ActionKey { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Task { get; init; }
    public IReadOnlyList<string>? Answers { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? PredicateParameters { get; init; }
    public string? PredicateKey { get; init; }
    public string? Verdict { get; init; }
}
