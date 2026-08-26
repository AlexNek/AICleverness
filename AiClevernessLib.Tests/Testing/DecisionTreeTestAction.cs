using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeTestAction : IDecisionAction
{
    public string Name => "collect";

    public Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default)
    {
        context.Data.Add(
            new DecisionData
            {
                Id = "evidence-1",
                Source = "test",
                Type = "evidence",
                Content = "deterministic evidence",
                CreatedAt = DateTimeOffset.UtcNow,
                ActionId = Name
            });
        return Task.FromResult(new DecisionActionResult(null, null, DecisionActionStatus.Success));
    }
}
