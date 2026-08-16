namespace AiCleverness.Models;

/// <summary>
/// Raised when the runtime switches to a fallback model during execution.
/// </summary>
public sealed record ModelSwitchedAgentEvent : AgentEvent
{
    public override string EventType => "ModelSwitched";

    /// <summary>The model that was switched away from.</summary>
    public required string From { get; init; }

    /// <summary>Human-readable reason for the switch.</summary>
    public required string Reason { get; init; }

    /// <summary>The model that was switched to.</summary>
    public required string To { get; init; }

    /// <summary>The turn number when the switch occurred (0-based).</summary>
    public required int Turn { get; init; }
}
