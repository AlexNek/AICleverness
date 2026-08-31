namespace AiCleverness.Models;

/// <summary>A single node in a workflow graph.</summary>
public sealed record WorkflowNode
{
    public IReadOnlyList<string> Children { get; init; } = Array.Empty<string>();
    public string? Condition { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
    public AgentRequest? Request { get; init; }
    public required WorkflowNodeType Type { get; init; }
}
