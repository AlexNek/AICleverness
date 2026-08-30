using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Built-in predicate that checks whether a state property is non-null.</summary>
public sealed class PropertyExistsPredicate : IDecisionPredicate
{
    public string Key => "propertyExists";

    public bool Evaluate(DecisionPredicateContext context)
    {
        var key = GetString(context.Parameters, "key");
        return key is not null
               && context.State.Properties.TryGetValue(key, out var value)
               && value is not null;
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> parameters, string key)
        => parameters.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
