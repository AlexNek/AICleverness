using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Quality gate that rejects answers shorter than a minimum length and asks for a retry.
/// </summary>
public sealed class MinimumLengthGate : IAgentQualityGate
{
    private const int MinimumOutputLength = 20;

    public string Name => "minimum-length";

    public int Priority => 100;

    public bool AppliesTo(IAgentContext context) => true;

    /// <inheritdoc />
    public Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        var longEnough = result.Output is { Length: >= MinimumOutputLength };

        return Task.FromResult(
            longEnough
                ? new QualityGateResult(true)
                : new QualityGateResult(
                    false,
                    Retry: true,
                    Reason: $"Answer is shorter than {MinimumOutputLength} characters."));
    }
}
