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
        var actionProperties = (IReadOnlyDictionary<string, string>?)null;
        if (context.TemplateParameters.TryGetValue("state-property", out var stateProperty))
        {
            context.State.Properties["directProperty"] = stateProperty;
            context.State.Properties["nullProperty"] = null;
            actionProperties = new Dictionary<string, string>
            {
                ["returnedProperty"] = stateProperty
            };
        }

        if (context.TemplateParameters.ContainsKey("state-collision"))
        {
            context.State.Properties["collision-first-secret"] = "first-value";
            context.State.Properties["collision-second-secret"] = "second-value";
        }

        if (context.TemplateParameters.ContainsKey("state-non-string"))
            context.State.Properties["numericProperty"] = 1234.5m;

        if (context.TemplateParameters.TryGetValue("state-long-key", out var longKeyValue))
            context.State.Properties["a-state-property-key-that-is-longer-than-the-configured-limit"] = longKeyValue;

        return Task.FromResult(new DecisionActionResult(
            null,
            actionProperties,
            DecisionActionStatus.Success));
    }
}
