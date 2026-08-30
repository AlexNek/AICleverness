using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeTestPredicate : IDecisionPredicate
{
    public string Key => "testPredicate";

    public bool Evaluate(DecisionPredicateContext context)
        => context.State.Properties.TryGetValue("allow", out var value) && value is true;
}
