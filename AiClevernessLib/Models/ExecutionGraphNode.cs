namespace AiCleverness.Models;

/// <summary>A node in the execution graph.</summary>
public sealed record ExecutionGraphNode(string Id, string Label, ExecutionGraphNodeType Type, DateTimeOffset? Timestamp = null);
