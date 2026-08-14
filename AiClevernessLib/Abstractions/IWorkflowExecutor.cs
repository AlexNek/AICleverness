using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Executes a workflow definition, orchestrating the execution of nodes
/// according to their dependencies and types.
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Executes the workflow and returns the combined result.
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default);
}
