using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Extended planner interface that exposes metadata and returns a structured <see cref="ExecutionPlan"/>.
/// Implementations that want to participate in the planner registry should implement this interface.
/// </summary>
/// <remarks>
/// This extends <see cref="IAgentPlanner"/> with a name, metadata, and structured plan output.
/// Existing <see cref="IAgentPlanner"/> implementations continue to work without changes.
/// </remarks>
public interface INamedAgentPlanner : IAgentPlanner
{
    /// <summary>Descriptive metadata about this planner.</summary>
    PlannerMetadata Metadata { get; }

    /// <summary>Unique name identifying this planner.</summary>
    string Name { get; }

    /// <summary>
    /// Produces a structured execution plan for achieving the goal.
    /// </summary>
    Task<ExecutionPlan> CreatePlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
