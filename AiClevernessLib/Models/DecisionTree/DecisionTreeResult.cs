using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Result of one decision-tree execution.</summary>
public sealed record DecisionTreeResult(
    string ExecutionId,
    bool Succeeded,
    string? Verdict,
    DecisionTreeOutcome Outcome,
    IReadOnlyList<DecisionClassification> Classifications,
    ResourceUsage Usage,
    string? Error = null,
    IReadOnlyDictionary<string, object>? StateProperties = null);
