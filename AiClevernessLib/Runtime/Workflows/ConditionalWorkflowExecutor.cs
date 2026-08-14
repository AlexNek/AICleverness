using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Workflows;

/// <summary>
/// Executes workflow nodes with support for conditional branching.
/// Condition nodes evaluate their expression against the current state to select a branch.
/// </summary>
public sealed class ConditionalWorkflowExecutor : IWorkflowExecutor
{
    private readonly Func<string, IReadOnlyDictionary<string, object>, bool>? _conditionEvaluator;

    private readonly IAgentRuntime _runtime;

    public string Name => "ConditionalWorkflow";

    /// <param name="runtime">The agent runtime for executing agent nodes.</param>
    /// <param name="conditionEvaluator">
    /// Optional evaluator for condition expressions. Receives the condition string and current context.
    /// Defaults to checking if a named result exists and was successful.
    /// </param>
    public ConditionalWorkflowExecutor(
        IAgentRuntime runtime,
        Func<string, IReadOnlyDictionary<string, object>, bool>? conditionEvaluator = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _conditionEvaluator = conditionEvaluator;
    }

    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var started = DateTimeOffset.UtcNow;
        var nodeResults = new Dictionary<string, AgentResult>();
        var nodesById = workflow.Nodes.ToDictionary(n => n.Id);
        var context = new Dictionary<string, object>(inputs ?? new Dictionary<string, object>());

        var queue = new Queue<string>();
        queue.Enqueue(workflow.EntryNodeId);
        var visited = new HashSet<string>();

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodeId = queue.Dequeue();
            if (!visited.Add(nodeId)) continue;
            if (!nodesById.TryGetValue(nodeId, out var node)) continue;

            switch (node.Type)
            {
                case WorkflowNodeType.Agent:
                    if (node.Request is not null)
                    {
                        var result = await _runtime.RunAsync(node.Request, null, cancellationToken);
                        nodeResults[node.Id] = result;
                        context[node.Id] = result.Success;
                        if (!result.Success)
                        {
                            return new WorkflowResult
                                       {
                                           Success = false,
                                           Error =
                                               result.Reasoning ?? $"Node '{node.Name}' failed.",
                                           NodeResults = nodeResults,
                                           Duration = DateTimeOffset.UtcNow - started
                                       };
                        }
                    }

                    // Enqueue children.
                    foreach (var child in node.Children)
                        queue.Enqueue(child);
                    break;

                case WorkflowNodeType.Condition:
                    var conditionMet = EvaluateCondition(node.Condition, context);
                    if (conditionMet && node.Children.Count > 0)
                        queue.Enqueue(node.Children[0]); // True branch
                    else if (!conditionMet && node.Children.Count > 1)
                        queue.Enqueue(node.Children[1]); // False branch
                    break;

                default:
                    foreach (var child in node.Children)
                        queue.Enqueue(child);
                    break;
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

    private bool EvaluateCondition(string? condition, IReadOnlyDictionary<string, object> context)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        if (_conditionEvaluator is not null)
            return _conditionEvaluator(condition, context);

        // Default: check if the condition is a node ID whose result was successful.
        return context.TryGetValue(condition, out var val) && val is true;
    }
}
