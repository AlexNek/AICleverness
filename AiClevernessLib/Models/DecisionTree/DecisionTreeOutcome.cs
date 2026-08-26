namespace AiCleverness.Models.DecisionTree;

/// <summary>Final outcome of a decision-tree execution.</summary>
public enum DecisionTreeOutcome
{
    Terminal,
    Unknown,
    ActionFailed,
    BudgetExhausted,
    Cancelled,
    ValidationFailed
}
