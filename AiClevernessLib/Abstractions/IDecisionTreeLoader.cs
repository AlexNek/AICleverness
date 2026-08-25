using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>Loads and validates declarative decision trees.</summary>
public interface IDecisionTreeLoader
{
    DecisionTreeModel Load(string json, CancellationToken cancellationToken = default);
}
