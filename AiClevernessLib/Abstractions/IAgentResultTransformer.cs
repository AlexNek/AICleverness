using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Transforms an approved agent result before it is returned to the caller.
/// </summary>
public interface IAgentResultTransformer
{
    string Name { get; }

    int Priority { get; }

    Task<AgentResult> TransformAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken);
}
