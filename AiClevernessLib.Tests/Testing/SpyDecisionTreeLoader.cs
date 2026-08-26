using AiCleverness.Abstractions;
using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class SpyDecisionTreeLoader : IDecisionTreeLoader
{
    public bool ValidateCalled { get; private set; }

    public DecisionTreeModel Load(string json, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public DecisionTreeModel Validate(DecisionTreeModel tree)
    {
        ValidateCalled = true;
        return tree;
    }
}