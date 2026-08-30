namespace AiCleverness.Models;

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
