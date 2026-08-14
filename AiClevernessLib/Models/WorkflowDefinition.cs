namespace AiCleverness.Models;

/// <summary>
/// Defines a complete workflow as a graph of nodes.
/// </summary>
public sealed record WorkflowDefinition
{
    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>ID of the entry node (first to execute).</summary>
    public required string EntryNodeId { get; init; }

    /// <summary>Unique identifier for this workflow.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>All nodes in this workflow.</summary>
    public required IReadOnlyList<WorkflowNode> Nodes { get; init; }

    /// <summary>Version of this workflow definition.</summary>
    public string? Version { get; init; }
}
