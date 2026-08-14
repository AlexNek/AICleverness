using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Produces a plan (ordered sequence of steps) for achieving a goal.
/// </summary>
public interface IAgentPlanner
{
    Task<IReadOnlyList<PlannedStep>> PlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
