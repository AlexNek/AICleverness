using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution and prompt limits for a decision tree.</summary>
public sealed record DecisionBudget
{
    public int MaxNodeVisits { get; init; } = 20;
    public int MaxLlmCalls { get; init; } = 10;
    public TimeSpan MaxElapsedTime { get; init; } = TimeSpan.FromSeconds(120);
    public int MaxContextTokens { get; init; } = 4000;
    public ResourceLimitAction OnExceeded { get; init; } = ResourceLimitAction.Halt;
}
