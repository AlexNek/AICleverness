using System.Text.Json;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution-scoped input supplied to a decision predicate.</summary>
public sealed record DecisionPredicateContext(
    string NodeId,
    DecisionState State,
    DataStore Data,
    IReadOnlyDictionary<string, JsonElement> Parameters);
