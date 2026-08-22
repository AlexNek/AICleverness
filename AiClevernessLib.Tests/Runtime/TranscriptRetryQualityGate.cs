using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Runtime;

public sealed class TranscriptRetryQualityGate : IAgentQualityGate
{
    public string Name => "TranscriptRetry";

    public int Priority => 0;

    public bool AppliesTo(IAgentContext context) => true;

    public Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        var approved = string.Equals(result.Output, "good answer", StringComparison.Ordinal);
        return Task.FromResult(new QualityGateResult(approved, !approved, "Use the good answer."));
    }
}
