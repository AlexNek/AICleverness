using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution context supplied to a decision-data selection policy.</summary>
public sealed record DecisionDataSelectionContext(
    DecisionTreeModel Tree,
    DecisionNode ClassifyNode,
    DecisionState State,
    IReadOnlyDictionary<string, string> TemplateParameters);
