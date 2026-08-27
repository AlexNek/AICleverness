using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>
/// Builds provider-neutral messages for a classify node from a read-only, policy-filtered snapshot.
/// Implementations should use the bounded display fields when rendering stable identifiers.
/// </summary>
public interface IDecisionLlmContextBuilder
{
    IReadOnlyList<LlmMessage> Build(
        DecisionTreeModel tree,
        DecisionNode classifyNode,
        DecisionState state,
        DecisionDataSnapshot data,
        IReadOnlyDictionary<string, string> templateParameters);
}
