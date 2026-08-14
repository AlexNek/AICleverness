namespace AiCleverness.Models;

/// <summary>
/// Estimated resource requirements for an execution.
/// Used for pre-flight checks and budget enforcement.
/// </summary>
public sealed record ResourceEstimate
{
    /// <summary>Confidence level of the estimate (0.0 to 1.0).</summary>
    public double Confidence { get; init; }

    /// <summary>Estimated monetary cost.</summary>
    public decimal? EstimatedCost { get; init; }

    /// <summary>Estimated execution time.</summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>Estimated input tokens to be consumed.</summary>
    public int? EstimatedInputTokens { get; init; }

    /// <summary>Estimated output tokens to be generated.</summary>
    public int? EstimatedOutputTokens { get; init; }

    /// <summary>Estimated number of tool calls.</summary>
    public int? EstimatedToolCalls { get; init; }

    /// <summary>Estimated number of LLM turns.</summary>
    public int? EstimatedTurns { get; init; }
}
