namespace AiCleverness.Models;

/// <summary>
/// Describes the capabilities of a model/provider endpoint.
/// Used for matching against <see cref="CapabilityRequirements"/>.
/// </summary>
public sealed record CapabilityProfile
{
    /// <summary>Capabilities in shared structured form. Used by new capability-based resolver.</summary>
    public Capabilities Capabilities { get; init; } = new();

    /// <summary>Unique identifier for this profile (e.g., "fast-text-code").</summary>
    public required string Id { get; init; }

    /// <summary>Whether this profile is currently available/enabled.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Display name (e.g., "Best value for most tasks").</summary>
    public required string Name { get; init; }

    /// <summary>Priority for selection when multiple profiles match (higher = preferred).</summary>
    public int Priority { get; init; }

    /// <summary>Custom properties for extension scenarios.</summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Capability tags (e.g., "code", "reasoning", "creative", "multilingual").</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Typical latency in milliseconds for first token.</summary>
    public int? TypicalLatencyMs { get; init; }
}
