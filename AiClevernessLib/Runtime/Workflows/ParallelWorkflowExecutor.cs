using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Workflows;

/// <summary>
/// Executes independent workflow nodes in parallel.
/// Nodes with no unresolved dependencies run concurrently.
/// </summary>
public sealed class ParallelWorkflowExecutor : IWorkflowExecutor
{
    private readonly int _maxConcurrency;

    private readonly IAgentRuntime _runtime;

    public string Name => "ParallelWorkflow";

    /// <param name="runtime">The agent runtime for executing agent nodes.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent node executions (default: unlimited).</param>
    public ParallelWorkflowExecutor(IAgentRuntime runtime, int maxConcurrency = int.MaxValue)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : int.MaxValue;
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
        var completed = new HashSet<string>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find nodes ready to execute (all dependencies completed).
            var ready = workflow.Nodes
                .Where(n => !completed.Contains(n.Id))
                .Where(n => n.Type == WorkflowNodeType.Agent && n.Request is not null)
                .Where(n => n.DependsOn.All(d => completed.Contains(d)))
                .Take(_maxConcurrency)
                .ToList();

            if (ready.Count == 0)
                break;

            // Execute ready nodes in parallel.
            using var semaphore = new SemaphoreSlim(_maxConcurrency);
            var tasks = ready.Select(async node =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await _runtime.RunAsync(
                                         node.Request!,
                                         null,
                                         cancellationToken);
                        return (node.Id, Result: result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var (id, result) in results)
            {
                nodeResults[id] = result;
                completed.Add(id);

                if (!result.Success)
                {
                    return new WorkflowResult
                               {
                                   Success = false,
                                   Error = result.Reasoning ?? $"Node '{id}' failed.",
                                   NodeResults = nodeResults,
                                   Duration = DateTimeOffset.UtcNow - started
                               };
                }
            }
        }

        // Also mark non-agent nodes as completed.
        foreach (var node in workflow.Nodes.Where(n => n.Type != WorkflowNodeType.Agent))
            completed.Add(node.Id);

        var lastResult = nodeResults.Values.LastOrDefault();
        return new WorkflowResult
                   {
                       Success = true,
                       Output = lastResult?.Output,
                       NodeResults = nodeResults,
                       Duration = DateTimeOffset.UtcNow - started
                   };
    }
}
