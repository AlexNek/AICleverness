using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Result of one decision-tree execution.</summary>
/// <param name="ExecutionId">Identifier assigned to this execution.</param>
/// <param name="Succeeded">Whether the execution reached a successful terminal outcome.</param>
/// <param name="Verdict">Verdict produced by the terminal node, if one was reached.</param>
/// <param name="Outcome">Outcome category produced by the execution.</param>
/// <param name="Classifications">Classifications captured while evaluating the tree.</param>
/// <param name="Usage">Resources consumed during the execution.</param>
/// <param name="Error">Error information when execution does not complete normally.</param>
public sealed record DecisionTreeResult(
    string ExecutionId,
    bool Succeeded,
    string? Verdict,
    DecisionTreeOutcome Outcome,
    IReadOnlyList<DecisionClassification> Classifications,
    ResourceUsage Usage,
    string? Error = null)
{
    /// <summary>Execution-scoped properties produced by decision actions.</summary>
    public IReadOnlyDictionary<string, object>? StateProperties { get; init; }
}
