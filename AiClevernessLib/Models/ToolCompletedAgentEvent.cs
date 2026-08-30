namespace AiCleverness.Models;

/// <summary>Raised when a tool invocation completes.</summary>
public sealed record ToolCompletedAgentEvent : AgentEvent
{
    /// <summary>How long the tool call took.</summary>
    public required TimeSpan Duration { get; init; }

    public override string EventType => "ToolCompleted";

    /// <summary>The tool execution result.</summary>
    public required ToolResult Result { get; init; }

    /// <summary>The name of the tool that completed.</summary>
    public required string ToolName { get; init; }
}
