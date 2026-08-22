using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Workflows;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates the workflow engine — composing multiple agent runs into a
/// directed acyclic graph (DAG) with explicit dependencies.
///
/// What this shows:
///   - Two nodes: "draft" and "review". The review node depends on the draft.
///   - The workflow executor runs draft first, then review — respecting the
///     dependency order automatically.
///   - Each node is a full agent run (with its own LLM call, tool access, etc.).
///   - In production, use workflows for multi-step pipelines: research → draft →
///     review → publish, or any process where steps have data dependencies.
/// </summary>
internal static class WorkflowScenario
{
    private const string DraftNodeId = "draft";

    private const string ReviewNodeId = "review";

    private const string WorkflowId = "content-review";

    public static async Task RunAsync(
        IServiceProvider provider,
        DemoTranscriptOptions transcriptOptions)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Script one answer per workflow node (each node makes one LLM call).
        llm.EnqueueText("Draft: Ship the refactor behind a feature flag.");
        llm.EnqueueText("Review: Approved — the draft is concise and actionable.");

        await using var scoped = DemoHost.CreateProvider(
            llm,
            transcriptOptions: transcriptOptions);

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var executor = new SequentialWorkflowExecutor(runtime);
        var workflow = BuildWorkflow(transcriptOptions);

        var result = await executor.ExecuteAsync(workflow);

        Console.WriteLine($"  Success: {result.Success}");
        Console.WriteLine($"  Output:  {result.Output}");
        Console.WriteLine("  Node results:");
        foreach (var (nodeId, nodeResult) in result.NodeResults)
        {
            Console.WriteLine($"    '{nodeId}': {nodeResult.Output}");
        }
    }

    private static WorkflowDefinition BuildWorkflow(DemoTranscriptOptions transcriptOptions) => new()
    {
        Id = WorkflowId,
        Name = "Draft and review",
        EntryNodeId = DraftNodeId,
        Nodes =
        [
            new WorkflowNode
            {
                Id = DraftNodeId,
                Name = "Draft",
                Type = WorkflowNodeType.Agent,
                Request = transcriptOptions.Apply(
                    new AgentRequest("Draft one release-note sentence."))
            },
            new WorkflowNode
            {
                Id = ReviewNodeId,
                Name = "Review",
                Type = WorkflowNodeType.Agent,
                DependsOn = [DraftNodeId],
                Request = transcriptOptions.Apply(
                    new AgentRequest("Review the draft for clarity."))
            }
        ]
    };
}
