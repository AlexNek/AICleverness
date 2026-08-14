namespace AiCleverness.Models;

/// <summary>
/// Result of a single decision evaluation.
/// </summary>
public sealed record DecisionResult(
    string Decision,
    bool Approved,
    double Confidence,
    string? Reasoning = null);
