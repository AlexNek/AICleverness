namespace AiCleverness.Models;

public enum EQualityTier
{
    Economy,

    Standard,

    High,

    Premium
}

public enum ECostTier
{
    Free,

    Cheap,

    Optimal,

    Expensive
}

/// <summary>
/// Flags representing model capabilities and modalities.
/// </summary>
[Flags]
public enum EModelCapability
{
    None = 0,

    // --- Text & Core LLM ---
    TextGeneration = 1 << 0,

    StructuredOutput = 1 << 1,

    ToolCalling = 1 << 2,

    Embedding = 1 << 3,

    Reranker = 1 << 4,

    // --- Vision (Image) ---
    ImageRecognition = 1 << 5,

    ImageGeneration = 1 << 6,

    // --- Audio ---
    AudioRecognition = 1 << 7,

    TextToSpeech = 1 << 8,

    AudioGeneration = 1 << 9,

    // --- Video ---
    VideoTranscription = 1 << 10,

    VideoRecognition = 1 << 11,

    VideoGeneration = 1 << 12
}

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
