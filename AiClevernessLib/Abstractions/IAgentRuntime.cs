using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Orchestrates an agent run from request to result.
/// </summary>
public interface IAgentRuntime
{
    Task<AgentResult> RunAsync(
        AgentRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
