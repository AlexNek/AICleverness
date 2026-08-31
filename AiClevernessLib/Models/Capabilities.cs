namespace AiCleverness.Models;

/// <summary>
/// Shared type used by both profile (what a model offers) and requirements (what an agent needs).
/// Profile semantics: field value = actual capability of the model.
/// Requirements semantics: field value = minimum acceptable threshold.
/// Matching: required flags must be present in profile flags (or profile null = assume capable).
/// All fields nullable — null means "no constraint" on the requirements side,
/// and "not declared" on the profile side (assume capable when requested).
/// </summary>
public sealed record Capabilities
{
    /// <summary>Modality and feature flags. Null = no constraints (allow all).</summary>
    public EModelCapability? CapabilityFlags { get; init; }

    public ECostTier? CostTier { get; init; }

    public int? MaxLatencyMs { get; init; }

    // Technical constraints (ranges, not booleans)
    public int? MinContextWindow { get; init; }

    public EQualityTier? QualityTier { get; init; }
}
