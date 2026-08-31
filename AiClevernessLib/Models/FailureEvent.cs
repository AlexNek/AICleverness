namespace AiCleverness.Models;

/// <summary>Raised when an unrecoverable error occurs during execution.</summary>
public sealed record FailureEvent : AgentEvent
{
    /// <summary>Description of the failure.</summary>
    public required string Error { get; init; }

    public override string EventType => "Failure";

    /// <summary>Whether the error is considered transient (might succeed on retry).</summary>
    public bool IsTransient { get; init; }

    /// <summary>Structured provider metadata when available.</summary>
    public LlmProviderFailureMetadata? ProviderFailure { get; init; }

    /// <summary>The phase during which the failure occurred.</summary>
    public string? Phase { get; init; }
}
