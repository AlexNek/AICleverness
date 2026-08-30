using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Built-in predicate that checks whether data of a type exists.</summary>
public sealed class DataExistsPredicate : IDecisionPredicate
{
    public string Key => "dataExists";

    public bool Evaluate(DecisionPredicateContext context)
    {
        if (!context.Parameters.TryGetValue("type", out var type) || type.ValueKind != JsonValueKind.String)
            return false;
        return context.Data.GetByType(type.GetString() ?? string.Empty).Count > 0;
    }
}
