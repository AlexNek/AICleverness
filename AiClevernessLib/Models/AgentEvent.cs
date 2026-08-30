namespace AiCleverness.Models;

/// <summary>
/// Base class for all streaming agent events.
/// Events are emitted during execution and represent observable state transitions.
/// </summary>
public abstract record AgentEvent
{
    /// <summary>Discriminator identifying the event type.</summary>
    public abstract string EventType { get; }

    /// <summary>Execution identifier this event belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>UTC timestamp when the event was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when the execution starts.</summary>
public sealed record RunStartedEvent : AgentEvent
{
    public override string EventType => "RunStarted";

    /// <summary>The original request.</summary>
    public required AgentRequest Request { get; init; }
}

/// <summary>Raised when the execution completes (success or failure).</summary>
public sealed record RunCompletedEvent : AgentEvent
{
    /// <summary>Total execution duration.</summary>
    public required TimeSpan Duration { get; init; }

    public override string EventType => "RunCompleted";

    /// <summary>The final result.</summary>
    public required AgentResult Result { get; init; }
}

/// <summary>Raised when a text chunk is received from the model during streaming.</summary>
public sealed record ModelChunkEvent : AgentEvent
{
    /// <summary>The text content of this chunk.</summary>
    public required string Content { get; init; }

    public override string EventType => "ModelChunk";

    /// <summary>True if this is the final chunk in the current turn.</summary>
    public bool IsFinal { get; init; }

    /// <summary>The current turn number (0-based).</summary>
    public int Turn { get; init; }
}

/// <summary>Raised when a tool invocation begins.</summary>
public sealed record ToolStartedEvent : AgentEvent
{
    public override string EventType => "ToolStarted";

    /// <summary>The invocation details.</summary>
    public required ToolInvocation Invocation { get; init; }

    /// <summary>The name of the tool being invoked.</summary>
    public required string ToolName { get; init; }
}

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

/// <summary>Raised when execution is cancelled.</summary>
public sealed record CancellationEvent : AgentEvent
{
    public override string EventType => "Cancelled";

    /// <summary>Optional reason for cancellation.</summary>
    public string? Reason { get; init; }
}

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

/// <summary>Raised when a policy blocks execution.</summary>
public sealed record PolicyBlockedAgentEvent : AgentEvent
{
    public override string EventType => "PolicyBlocked";

    /// <summary>Name of the blocking policy.</summary>
    public required string PolicyName { get; init; }

    /// <summary>Reason for blocking.</summary>
    public string? Reason { get; init; }
}

/// <summary>Raised at the start of each LLM turn.</summary>
public sealed record TurnStartedEvent : AgentEvent
{
    public override string EventType => "TurnStarted";

    /// <summary>The turn number (0-based).</summary>
    public required int Turn { get; init; }
}
