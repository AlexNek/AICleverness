using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class FixedDecisionLlmContextBuilder : IDecisionLlmContextBuilder
{
    private readonly IReadOnlyList<LlmMessage> _messages;

    public FixedDecisionLlmContextBuilder(IReadOnlyList<LlmMessage> messages)
    {
        _messages = messages;
    }

    public IReadOnlyList<LlmMessage> Build(
        DecisionTreeModel tree,
        DecisionNode classifyNode,
        DecisionState state,
        DataStore data,
        IReadOnlyDictionary<string, string> templateParameters)
        => _messages;
}
