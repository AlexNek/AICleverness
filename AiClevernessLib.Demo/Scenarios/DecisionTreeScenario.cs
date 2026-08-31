using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using Microsoft.Extensions.DependencyInjection;
using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>Demonstrates a generic in-memory evidence decision tree.</summary>
internal static class DecisionTreeScenario
{
    private const string DemoTask = """
        Decide whether this release note can be published.

        Release note:
        Version 1.4.0 adds decision-tree transcript diagnostics. Users can inspect the task, LLM input and output, parsed decision, evidence, and selected path in a Markdown debug transcript.

        Publication criteria:
        1. The note describes a user-visible change.
        2. The note is concise.
        3. The note contains no secrets or private data.
        4. The note has no unresolved blocking issues.
        """;

    public static async Task RunAsync(
        IServiceProvider provider,
        DemoTranscriptOptions? transcriptOptions = null)
    {
        var existingTranscripts = transcriptOptions?.Enabled == true
                                  && Directory.Exists(transcriptOptions.Directory)
            ? Directory.GetFiles(transcriptOptions.Directory, "*.md")
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.EnqueueText("{\"answer\":\"supported\",\"observation\":\"The release note describes the user-visible change, meets all four publication criteria, and has no unresolved blockers.\",\"confidence\":\"high\"}");
        var executor = provider.GetRequiredService<DecisionTreeExecutor>();
        var actions = new IDecisionAction[] { new DecisionCollectEvidenceAction() };
        var result = await executor.ExecuteAsync(actions, CreateTree());

        Console.WriteLine($"Decision task: {DemoTask}");
        Console.WriteLine($"Decision outcome: {result.Outcome}; verdict: {result.Verdict}; error: {result.Error}; node visits: {result.Usage.NodeVisits}; LLM calls: {result.Usage.LlmCalls}");

        if (transcriptOptions?.Enabled == true && Directory.Exists(transcriptOptions.Directory))
        {
            var transcriptPath = Directory.GetFiles(transcriptOptions.Directory, "*.md")
                .Where(path => !existingTranscripts.Contains(path))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            Console.WriteLine($"Decision transcript: {transcriptPath ?? transcriptOptions.Directory}");
        }
    }

    private static DecisionTreeModel CreateTree()
        => new()
        {
            TreeId = "demo-evidence-tree",
            Version = 1,
            Task = DemoTask,
            SystemPrompt = "Review the task and evidence. Return supported only when the evidence demonstrates that every publication criterion is satisfied. Otherwise return unsupported. Return JSON with answer, observation, and confidence.",
            StartNodeId = "collect",
            Nodes = new Dictionary<string, DecisionNode>(StringComparer.Ordinal)
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionKey = "collectEvidence",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "classify" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Does the release note above satisfy every publication criterion?",
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
                    PredicateKey = "dataExists",
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
