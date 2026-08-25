namespace AiCleverness.Models.DecisionTree;

/// <summary>A labeled edge from one decision node to another.</summary>
public sealed record DecisionTransition
{
    public required string Condition { get; init; }
    public required string NextNodeId { get; init; }
}
