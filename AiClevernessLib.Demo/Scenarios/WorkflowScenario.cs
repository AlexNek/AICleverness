using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Workflows;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Runs a two-node workflow: a draft agent followed by a review agent that depends on it.
/// </summary>
internal static class WorkflowScenario
{
    private const string DraftNodeId = "draft";

    private const string ReviewNodeId = "review";

    private const string WorkflowId = "content-review";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();
        llm.EnqueueText("Draft: Ship the refactor behind a feature flag.");
        llm.EnqueueText("Review: Approved — the draft is concise and actionable.");

        await using var scoped = DemoHost.CreateProvider(llm);

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var executor = new SequentialWorkflowExecutor(runtime);
        var workflow = BuildWorkflow();

        var result = await executor.ExecuteAsync(workflow);

        Console.WriteLine($"  Success: {result.Success}");
        Console.WriteLine($"  Output:  {result.Output}");
        Console.WriteLine("  Node results:");
        foreach (var (nodeId, nodeResult) in result.NodeResults)
        {
            Console.WriteLine($"    '{nodeId}': {nodeResult.Output}");
        }
    }

    private static WorkflowDefinition BuildWorkflow() => new()
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
                                                                         Request = new AgentRequest(
                                                                             "Draft one release-note sentence.")
                                                                     },
                                                                     new WorkflowNode
                                                                     {
                                                                         Id = ReviewNodeId,
                                                                         Name = "Review",
                                                                         Type = WorkflowNodeType.Agent,
                                                                         DependsOn = [DraftNodeId],
                                                                         Request = new AgentRequest(
                                                                             "Review the draft for clarity.")
                                                                     }
                                                                 ]
                                                             };
}
