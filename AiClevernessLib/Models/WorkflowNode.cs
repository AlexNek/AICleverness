namespace AiCleverness.Models;

/// <summary>
/// A single node in a workflow graph.
/// Nodes can be agent invocations, conditions, or control-flow elements.
/// </summary>
public sealed record WorkflowNode
{
    /// <summary>Child node IDs for composite nodes (parallel, sequential groups).</summary>
    public IReadOnlyList<string> Children { get; init; } = Array.Empty<string>();

    /// <summary>Condition expression for conditional nodes.</summary>
    public string? Condition { get; init; }

    /// <summary>IDs of nodes that must complete before this node starts.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

    /// <summary>Unique identifier for this node.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Custom properties for extension scenarios.</summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Agent request to execute (for agent nodes).</summary>
    public AgentRequest? Request { get; init; }

    /// <summary>Type of node (e.g., "agent", "condition", "parallel", "transform").</summary>
    public required WorkflowNodeType Type { get; init; }
}

/// <summary>
/// Type of workflow node.
/// </summary>
public enum WorkflowNodeType
{
    /// <summary>Executes an agent request.</summary>
    Agent,

    /// <summary>Evaluates a condition to choose a branch.</summary>
    Condition,

    /// <summary>Executes children in parallel.</summary>
    Parallel,

    /// <summary>Transforms the output of a previous node.</summary>
    Transform,

    /// <summary>Routes to a specific node based on input.</summary>
    Router
}
