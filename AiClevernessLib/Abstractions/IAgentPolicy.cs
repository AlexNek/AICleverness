using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// A heuristic or rule that evaluates the current context and returns a scored recommendation.
/// </summary>
public interface IAgentPolicy
{
    string Name { get; }

    int Priority { get; }

    bool AppliesTo(IAgentContext context);

    Task<PolicyResult> EvaluateAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
