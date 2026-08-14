namespace AiCleverness.Models;

/// <summary>
/// Result of a policy evaluation.
/// </summary>
/// <param name="Applied">True when the policy evaluated the context and produced a recommendation.</param>
/// <param name="Score">Numeric score assigned by the policy; semantics are policy-specific.</param>
/// <param name="Recommendation">Action recommendation, e.g. "allow" or "block".</param>
/// <param name="Reasoning">Human-readable explanation of the recommendation.</param>
public sealed record PolicyResult(
    bool Applied,
    double Score,
    string? Recommendation = null,
    string? Reasoning = null);
