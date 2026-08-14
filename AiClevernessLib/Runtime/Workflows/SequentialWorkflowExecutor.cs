using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Workflows;

/// <summary>
/// Executes workflow nodes sequentially in dependency order.
/// Each node's output is available to subsequent nodes.
/// </summary>
public sealed class SequentialWorkflowExecutor : IWorkflowExecutor
{
    private readonly IAgentRuntime _runtime;

    public string Name => "SequentialWorkflow";

    public SequentialWorkflowExecutor(IAgentRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var started = DateTimeOffset.UtcNow;
        var nodeResults = new Dictionary<string, AgentResult>();
        var ordered = TopologicalSort(workflow);

        foreach (var node in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node.Type != WorkflowNodeType.Agent || node.Request is null)
                continue;

            var result = await _runtime.RunAsync(node.Request, null, cancellationToken);
            nodeResults[node.Id] = result;

            if (!result.Success)
            {
                return new WorkflowResult
                           {
                               Success = false,
                               Output = result.Output,
                               Error = result.Reasoning ?? $"Node '{node.Name}' failed.",
                               NodeResults = nodeResults,
                               Duration = DateTimeOffset.UtcNow - started
                           };
            }
        }

        var lastResult = nodeResults.Values.LastOrDefault();
        return new WorkflowResult
                   {
                       Success = true,
                       Output = lastResult?.Output,
                       NodeResults = nodeResults,
                       Duration = DateTimeOffset.UtcNow - started
                   };
    }

    private static IReadOnlyList<WorkflowNode> TopologicalSort(WorkflowDefinition workflow)
    {
        var nodesById = workflow.Nodes.ToDictionary(n => n.Id);
        var visited = new HashSet<string>();
        var result = new List<WorkflowNode>();

        void Visit(string id)
        {
            if (!visited.Add(id)) return;
            if (!nodesById.TryGetValue(id, out var node)) return;
            foreach (var dep in node.DependsOn)
                Visit(dep);
            result.Add(node);
        }

        // Start from entry node, then visit remaining.
        Visit(workflow.EntryNodeId);
        foreach (var node in workflow.Nodes)
            Visit(node.Id);

        return result;
    }
}
