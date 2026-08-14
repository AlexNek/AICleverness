using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Evaluates a final agent result and may approve, reject, retry, or replace it.
/// </summary>
public interface IAgentQualityGate
{
    string Name { get; }

    int Priority { get; }

    bool AppliesTo(IAgentContext context);

    Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken);
}
