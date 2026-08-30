namespace AiCleverness.Models;

/// <summary>
/// One complete description of a single LLM call attempt.
/// Emitted via <c>IAgentObserver.OnLlmCallCompletedAsync</c> on every outcome
/// (success, error, timeout).
/// </summary>
public sealed record LlmCallInfo
{
    /// <summary>Attempt number within the failover chain (1-based).</summary>
    public required int Attempt { get; init; }

    /// <summary>Classification applied by the error classifier (null on success).</summary>
    public EFailureClassification? Classification { get; init; }

    /// <summary>Structured provider metadata when the call failed through an adapter.</summary>
    public LlmProviderFailureMetadata? ProviderFailure { get; init; }

    /// <summary>Wall-clock duration of this LLM call.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Error message if the call failed; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Execution this call belongs to.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>Whether this attempt used a fallback model.</summary>
    public bool IsFallback { get; init; }

    /// <summary>Model used for this attempt.</summary>
    public required string Model { get; init; }

    /// <summary>UTC timestamp when the call started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Whether the call completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Logical turn number (0-based).</summary>
    public required int Turn { get; init; }

    /// <summary>Token usage (null if the call failed before producing usage).</summary>
    public LlmTokenUsage? Usage { get; init; }
}
