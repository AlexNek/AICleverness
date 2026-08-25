using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using Microsoft.Extensions.DependencyInjection;
using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>Demonstrates a generic in-memory evidence decision tree.</summary>
public static class DecisionTreeScenario
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.EnqueueText("{\"answer\":\"supported\",\"observation\":\"The evidence is present.\",\"confidence\":\"high\"}");
        var executor = provider.GetRequiredService<DecisionTreeExecutor>();
        var result = await executor.ExecuteAsync(CreateTree());

        Console.WriteLine($"Decision outcome: {result.Outcome}; verdict: {result.Verdict}; error: {result.Error}; node visits: {result.Usage.NodeVisits}; LLM calls: {result.Usage.LlmCalls}");
    }

    private static DecisionTreeModel CreateTree()
        => new()
        {
            TreeId = "demo-evidence-tree",
            Version = 1,
            StartNodeId = "collect",
            Nodes = new Dictionary<string, DecisionNode>(StringComparer.Ordinal)
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionName = "collectEvidence",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "classify" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Is the evidence sufficient to support the request?",
                    Answers = ["supported", "unsupported"],
                    Transitions =
                    [
                        new() { Condition = "supported", NextNodeId = "hasEvidence" },
                        new() { Condition = "unsupported", NextNodeId = "unsupported" },
                        new() { Condition = "unknown", NextNodeId = "unknown" }
                    ]
                },
                ["hasEvidence"] = new()
                {
                    Type = EDecisionNodeType.Condition,
                    PredicateName = "dataExists",
                    PredicateParameters = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["type"] = System.Text.Json.JsonDocument.Parse("\"evidence\"").RootElement.Clone()
                    },
                    Transitions =
                    [
                        new() { Condition = "true", NextNodeId = "approved" },
                        new() { Condition = "false", NextNodeId = "failed" }
                    ]
                },
                ["approved"] = new() { Type = EDecisionNodeType.Terminal, Verdict = "supported" },
                ["unsupported"] = new() { Type = EDecisionNodeType.Terminal, Verdict = "unsupported" },
                ["unknown"] = new() { Type = EDecisionNodeType.Terminal, Verdict = "unknown" },
                ["failed"] = new() { Type = EDecisionNodeType.Terminal, Verdict = "failed" }
            }
        };
}
