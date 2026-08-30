namespace AiCleverness.Models;

/// <summary>Raised when a policy blocks execution.</summary>
public sealed record PolicyBlockedAgentEvent : AgentEvent
{
    public override string EventType => "PolicyBlocked";

    /// <summary>Name of the blocking policy.</summary>
    public required string PolicyName { get; init; }

    /// <summary>Reason for blocking.</summary>
    public string? Reason { get; init; }
}
