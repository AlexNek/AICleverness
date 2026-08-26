using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class ThrowingDecisionPredicate : IDecisionPredicate
{
    public string Name => "throwing";

    public bool Evaluate(DecisionPredicateContext context)
        => throw new InvalidOperationException("predicate failed");
}