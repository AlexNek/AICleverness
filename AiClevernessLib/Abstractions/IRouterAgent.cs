using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Routes an agent request to the appropriate downstream agent or handler.
/// Used in multi-agent workflows to dispatch work based on content analysis.
/// </summary>
public interface IRouterAgent
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Routes the request and returns the name/ID of the selected target.
    /// </summary>
    Task<RoutingDecision> RouteAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Decision produced by a router agent.
/// </summary>
public sealed record RoutingDecision(
    string TargetId,
    string? Reason = null,
    double Confidence = 1.0,
    IReadOnlyDictionary<string, object>? ModifiedParameters = null);
