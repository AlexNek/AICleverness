using AiCleverness.Models;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Result of one decision-tree execution.</summary>
public sealed record DecisionTreeResult
{
    /// <summary>Identifier assigned to this execution.</summary>
    public string ExecutionId { get; init; }

    /// <summary>Whether the execution reached a successful terminal outcome.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Verdict produced by the terminal node, if one was reached.</summary>
    public string? Verdict { get; init; }

    /// <summary>Outcome category produced by the execution.</summary>
    public DecisionTreeOutcome Outcome { get; init; }

    /// <summary>Classifications captured while evaluating the tree.</summary>
    public IReadOnlyList<DecisionClassification> Classifications { get; init; }

    /// <summary>Resources consumed during the execution.</summary>
    public ResourceUsage Usage { get; init; }

    /// <summary>Error information when execution does not complete normally.</summary>
    public string? Error { get; init; }

    /// <summary>Execution-scoped properties produced by decision actions.</summary>
    public IReadOnlyDictionary<string, object>? StateProperties { get; init; }

    /// <summary>Creates a result for one decision-tree execution.</summary>
    public DecisionTreeResult(
        string ExecutionId,
        bool Succeeded,
        string? Verdict,
        DecisionTreeOutcome Outcome,
        IReadOnlyList<DecisionClassification> Classifications,
        ResourceUsage Usage,
        string? Error = null)
    {
        this.ExecutionId = ExecutionId;
        this.Succeeded = Succeeded;
        this.Verdict = Verdict;
        this.Outcome = Outcome;
        this.Classifications = Classifications;
        this.Usage = Usage;
        this.Error = Error;
    }

    /// <summary>Deconstructs the result using the original public result shape.</summary>
    public void Deconstruct(
        out string executionId,
        out bool succeeded,
        out string? verdict,
        out DecisionTreeOutcome outcome,
        out IReadOnlyList<DecisionClassification> classifications,
        out ResourceUsage usage,
        out string? error)
    {
        executionId = ExecutionId;
        succeeded = Succeeded;
        verdict = Verdict;
        outcome = Outcome;
        classifications = Classifications;
        usage = Usage;
        error = Error;
    }
}
