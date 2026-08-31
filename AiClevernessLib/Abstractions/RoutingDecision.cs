using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Decision produced by a router agent.
/// </summary>
public sealed record RoutingDecision(
    string TargetId,
    string? Reason = null,
    double Confidence = 1.0,
    IReadOnlyDictionary<string, object>? ModifiedParameters = null);
