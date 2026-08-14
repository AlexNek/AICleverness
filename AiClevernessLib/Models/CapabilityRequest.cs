namespace AiCleverness.Models;

/// <summary>
/// Describes the capabilities required for an execution.
/// Used by <see cref="AiCleverness.Abstractions.ICapabilityResolver"/> to select
/// the appropriate model/provider.
/// </summary>
public sealed record CapabilityRequest
{
    /// <summary>Maximum acceptable cost per 1K tokens. Null means no budget constraint.</summary>
    public decimal? MaxCostPer1KTokens { get; init; }

    /// <summary>Maximum acceptable latency in milliseconds. Null means no latency constraint.</summary>
    public int? MaxLatencyMs { get; init; }

    /// <summary>Minimum context window size in tokens. Null means no preference.</summary>
    public int? MinContextWindow { get; init; }

    /// <summary>Preferred model family (e.g., "gpt-4", "claude", "gemini"). Null means no preference.</summary>
    public string? PreferredFamily { get; init; }

    /// <summary>Custom properties for extension scenarios.</summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Required quality tier (e.g., "high", "medium", "fast"). Null means no preference.</summary>
    public string? QualityTier { get; init; }

    /// <summary>Custom capability tags required (e.g., "code", "reasoning", "creative").</summary>
    public IReadOnlyList<string> RequiredTags { get; init; } = Array.Empty<string>();

    /// <summary>Whether streaming is required.</summary>
    public bool RequiresStreaming { get; init; }

    /// <summary>Whether structured JSON output is required.</summary>
    public bool RequiresStructuredOutput { get; init; }

    /// <summary>Whether tool/function calling is required.</summary>
    public bool RequiresToolCalling { get; init; }

    /// <summary>Whether vision/image input is required.</summary>
    public bool RequiresVision { get; init; }
}
