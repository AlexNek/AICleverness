namespace AiCleverness.Models;

/// <summary>Raised at the start of each LLM turn.</summary>
public sealed record TurnStartedEvent : AgentEvent
{
    public override string EventType => "TurnStarted";

    /// <summary>The turn number (0-based).</summary>
    public required int Turn { get; init; }
}
