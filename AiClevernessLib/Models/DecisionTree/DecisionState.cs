using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Mutable state isolated to one decision-tree execution.</summary>
public sealed class DecisionState
{
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);
    public List<DecisionClassification> Classifications { get; } = [];
    public ResourceUsage ResourceUsage { get; } = new();
}
