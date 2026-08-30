namespace AiCleverness.Models.DecisionTree;

/// <summary>Declarative decision tree executed by the decision-tree executor.</summary>
public sealed record DecisionTree
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DecisionBudget Budget { get; init; } = new();
    public IReadOnlyDictionary<string, DecisionNode> Nodes { get; init; } =
        new Dictionary<string, DecisionNode>(StringComparer.Ordinal);
    public required string StartNodeId { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Task { get; init; }
    public required string TreeId { get; init; }
    public int Version { get; init; }
}
