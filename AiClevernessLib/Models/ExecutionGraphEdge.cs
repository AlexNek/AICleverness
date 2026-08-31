namespace AiCleverness.Models;

/// <summary>A directed edge in the execution graph.</summary>
public sealed record ExecutionGraphEdge(string From, string To, string? Label = null);
