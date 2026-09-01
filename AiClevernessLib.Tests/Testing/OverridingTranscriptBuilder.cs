using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.Transcript;

namespace AiClevernessLib.Tests.Testing;

public sealed class OverridingTranscriptBuilder : TranscriptBuilderDecorator
{
    public override string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData)
        => base.DecisionAction(
                nodeId,
                actionKey,
                nodeName,
                status,
                outcomeSummary,
                error,
                producedData)
            .Replace("### Decision action:", "### Custom action:", StringComparison.Ordinal);
}
