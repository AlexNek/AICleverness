namespace AiCleverness.Models;

/// <summary>
/// Result of a quality gate evaluation.
/// </summary>
public sealed record QualityGateResult(
    bool Approved,
    bool Retry = false,
    string? Reason = null,
    AgentResult? ReplacementResult = null);
