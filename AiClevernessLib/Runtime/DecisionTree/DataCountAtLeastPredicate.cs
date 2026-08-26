using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Built-in predicate that checks a minimum number of data records.</summary>
public sealed class DataCountAtLeastPredicate : IDecisionPredicate
{
    public string Name => "dataCountAtLeast";

    public bool Evaluate(DecisionPredicateContext context)
    {
        if (!context.Parameters.TryGetValue("type", out var type)
            || type.ValueKind != JsonValueKind.String
            || !context.Parameters.TryGetValue("min", out var minimum)
            || !minimum.TryGetInt32(out var count)
            || count < 0)
            return false;
        return context.Data.GetByType(type.GetString() ?? string.Empty).Count >= count;
    }
}
