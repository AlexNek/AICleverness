namespace AiCleverness.Models.DecisionTree;

/// <summary>Outcome reported by an application decision action.</summary>
public enum DecisionActionStatus
{
    Success,
    TransientFailure,
    PermanentFailure
}
