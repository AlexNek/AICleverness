using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingDecisionLlmContextBuilder : IDecisionLlmContextBuilder
{
    private readonly DefaultDecisionLlmContextBuilder _inner = new();

    public DecisionDataSnapshot? Data { get; private set; }

    public IReadOnlyList<LlmMessage> Build(
        DecisionTreeModel tree,
        DecisionNode classifyNode,
        DecisionState state,
        DecisionDataSnapshot data,
        IReadOnlyDictionary<string, string> templateParameters)
    {
        Data = data;
        return _inner.Build(tree, classifyNode, state, data, templateParameters);
    }
}
