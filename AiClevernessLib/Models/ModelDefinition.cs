namespace AiCleverness.Models;

/// <summary>
/// Describes a model identity. No pricing, no rate limits, no capability flags —
/// those are already on <see cref="CapabilityProfile.Capabilities"/> and change independently.
/// </summary>
public sealed record ModelDefinition
{
    /// <summary>Provider-qualified model name (e.g. "gemini-2.0-flash-001").</summary>
    public required string Name { get; init; }

    /// <summary>Stable provider identifier (e.g. "google", "anthropic", "openai").</summary>
    /// <remarks>Not exposed to runtime consumers — used by provider resolution layer.</remarks>
    public required string ProviderKey { get; init; }
}
