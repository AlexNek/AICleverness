using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// A discrete decision the agent can make, optionally backed by an LLM or deterministic code.
/// </summary>
public interface IAgentDecision
{
    string Name { get; }

    bool AppliesTo(IAgentContext context);

    Task<DecisionResult> EvaluateAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
