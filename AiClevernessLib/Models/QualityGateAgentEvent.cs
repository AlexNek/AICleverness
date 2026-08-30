namespace AiCleverness.Models;

/// <summary>Raised when a quality gate evaluates the result.</summary>
public sealed record QualityGateAgentEvent : AgentEvent
{
    /// <summary>Whether the gate approved the result.</summary>
    public required bool Approved { get; init; }

    public override string EventType => "QualityGate";

    /// <summary>Name of the quality gate.</summary>
    public required string GateName { get; init; }

    /// <summary>The gate's reason for rejection, if any.</summary>
    public string? Reason { get; init; }

    /// <summary>Whether a retry was requested.</summary>
    public bool Retry { get; init; }

    /// <summary>The retry attempt number when rejected.</summary>
    public int RetryCount { get; init; }
}
