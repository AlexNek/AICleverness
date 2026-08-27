namespace AiCleverness.Models.DecisionTree;

/// <summary>Identifies the operation performed by a decision-tree node.</summary>
public enum EDecisionNodeType
{
    Action,
    Classify,
    Condition,
    Terminal
}
