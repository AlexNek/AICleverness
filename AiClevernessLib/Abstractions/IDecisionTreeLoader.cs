using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>Loads and validates declarative decision trees.</summary>
public interface IDecisionTreeLoader
{
    DecisionTreeModel Load(string json, CancellationToken cancellationToken = default);

    /// <summary>Validates a materialized tree before execution.</summary>
    DecisionTreeModel Validate(DecisionTreeModel tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree;
    }
}
