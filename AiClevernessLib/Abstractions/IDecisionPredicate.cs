using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>Application or library predicate evaluated by a condition node.</summary>
public interface IDecisionPredicate
{
    string Key { get; }
    bool Evaluate(DecisionPredicateContext context);
}
