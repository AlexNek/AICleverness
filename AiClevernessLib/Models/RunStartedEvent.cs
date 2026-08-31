namespace AiCleverness.Models;

/// <summary>Raised when the execution starts.</summary>
public sealed record RunStartedEvent : AgentEvent
{
    public override string EventType => "RunStarted";

    /// <summary>The original request.</summary>
    public required AgentRequest Request { get; init; }
}
