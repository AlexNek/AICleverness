using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Built-in predicate that compares a state property to a string value.</summary>
public sealed class PropertyEqualsPredicate : IDecisionPredicate
{
    public string Key => "propertyEquals";

    public bool Evaluate(DecisionPredicateContext context)
    {
        if (!context.Parameters.TryGetValue("key", out var key)
            || key.ValueKind != JsonValueKind.String
            || !context.Parameters.TryGetValue("value", out var expected)
            || expected.ValueKind != JsonValueKind.String
            || !context.State.Properties.TryGetValue(key.GetString() ?? string.Empty, out var actual))
            return false;
        return string.Equals(actual?.ToString(), expected.GetString(), StringComparison.Ordinal);
    }
}
