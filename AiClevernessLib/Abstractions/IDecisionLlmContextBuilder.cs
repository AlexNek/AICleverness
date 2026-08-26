using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>Builds provider-neutral messages for a question node.</summary>
public interface IDecisionLlmContextBuilder
{
    IReadOnlyList<LlmMessage> Build(
        DecisionTreeModel tree,
        DecisionNode questionNode,
        DecisionState state,
        DataStore data,
        IReadOnlyDictionary<string, string> templateParameters);
}
