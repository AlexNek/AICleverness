using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// An autonomous agent that executes a goal within a shared context.
/// </summary>
public interface IAgent
{
    string Name { get; }

    /// <summary>
    /// Determines the capability requirements for this execution.
    /// Called by the runtime before resolving the model profile.
    /// Return null to skip capability-based resolution (use default profile).
    /// </summary>
    CapabilityRequirements? DetermineCapabilities(AgentRequest request);

    Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
