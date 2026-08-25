namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution-scoped input supplied to a decision action.</summary>
public sealed record DecisionActionContext(
    string NodeId,
    string ExecutionId,
    IReadOnlyDictionary<string, string> TemplateParameters,
    DecisionState State,
    DataStore Data);
