using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeTestAction : IDecisionAction
{
    public string Key => "collect";

    public Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default)
    {
        var content = context.TemplateParameters.TryGetValue("evidence-content", out var configuredContent)
            ? configuredContent
            : "deterministic evidence";
        context.Data.Add(
            new DecisionData
            {
                Id = "evidence-1",
                Source = "test",
                Type = "evidence",
                Content = content,
                CreatedAt = DateTimeOffset.UtcNow,
                ActionId = Key
            });
        return Task.FromResult(new DecisionActionResult(null, null, DecisionActionStatus.Success));
    }
}
