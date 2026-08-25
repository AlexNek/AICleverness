using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Demo;

/// <summary>Small in-memory demo action used only by the decision-tree scenario.</summary>
public sealed class DecisionCollectEvidenceAction : IDecisionAction
{
    public string Name => "collectEvidence";

    public Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Add(
            new DecisionData
            {
                Id = "demo-evidence",
                Source = "demo",
                Type = "evidence",
                Content = "deterministic in-memory evidence",
                CreatedAt = DateTimeOffset.UtcNow,
                ActionId = Name
            });
        return Task.FromResult(
            new DecisionActionResult(null, null, DecisionActionStatus.Success));
    }
}
