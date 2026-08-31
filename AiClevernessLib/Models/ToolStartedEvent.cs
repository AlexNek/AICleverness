namespace AiCleverness.Models;

/// <summary>Raised when a tool invocation begins.</summary>
public sealed record ToolStartedEvent : AgentEvent
{
    public override string EventType => "ToolStarted";

    /// <summary>The invocation details.</summary>
    public required ToolInvocation Invocation { get; init; }

    /// <summary>The name of the tool being invoked.</summary>
    public required string ToolName { get; init; }
}
