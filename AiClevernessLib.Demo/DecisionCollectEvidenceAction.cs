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
                Id = "sample-release-note-review",
                Source = "release-review",
                Type = "evidence",
                Content = "Review findings for the release note against all four publication criteria: the note describes the user-visible transcript diagnostics, is concise, contains no secrets or private data, and has no unresolved blocking issues. All criteria passed.",
                CreatedAt = DateTimeOffset.UtcNow,
                ActionId = Name,
                Metadata = new Dictionary<string, string>
                {
                    ["subject"] = "sample release note",
                    ["reviewStatus"] = "all criteria passed",
                    ["criteriaChecked"] = "4"
                }
            });
        return Task.FromResult(
            new DecisionActionResult(null, null, DecisionActionStatus.Success));
    }
}
