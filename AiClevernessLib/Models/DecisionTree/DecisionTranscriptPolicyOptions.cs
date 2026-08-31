namespace AiCleverness.Models.DecisionTree;

/// <summary>Limits applied to decision-specific transcript content after redaction.</summary>
public sealed class DecisionTranscriptPolicyOptions
{
    /// <summary>Maximum produced data items recorded for one action.</summary>
    public int MaxProducedItemsPerAction { get; set; } = 100;

    /// <summary>Maximum produced data content length per item.</summary>
    public int MaxContentLength { get; set; } = 4_000;

    /// <summary>Maximum metadata entries recorded for one produced item.</summary>
    public int MaxMetadataEntries { get; set; } = 20;

    /// <summary>Maximum metadata key length recorded for one produced item.</summary>
    public int MaxMetadataKeyLength { get; set; } = 256;

    /// <summary>Maximum metadata value length recorded for one produced item.</summary>
    public int MaxMetadataValueLength { get; set; } = 1_000;

    /// <summary>Maximum content length recorded for one prepared LLM message.</summary>
    public int MaxMessageContentLength { get; set; } = 8_000;

    /// <summary>Maximum state properties recorded in one decision result.</summary>
    public int MaxStateProperties { get; set; } = 100;

    /// <summary>Maximum state-property key length recorded in one decision result.</summary>
    public int MaxStatePropertyKeyLength { get; set; } = 256;

    /// <summary>Maximum state-property value length recorded in one decision result.</summary>
    public int MaxStatePropertyValueLength { get; set; } = 4_000;

    /// <summary>Maximum model response content length recorded for one LLM attempt.</summary>
    public int MaxResponseContentLength { get; set; } = 8_000;

    /// <summary>Optional total decision transcript character limit for one execution.</summary>
    public int? MaxTotalCharacters { get; set; } = 100_000;

    internal void Validate()
    {
        if (MaxProducedItemsPerAction <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxProducedItemsPerAction), "Maximum produced items per action must be positive.");
        if (MaxContentLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxContentLength), "Transcript content length must be positive.");
        if (MaxMetadataEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataEntries), "Maximum metadata entries must be positive.");
        if (MaxMetadataKeyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataKeyLength), "Metadata key length must be positive.");
        if (MaxMetadataValueLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataValueLength), "Metadata value length must be positive.");
        if (MaxMessageContentLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMessageContentLength), "Message content length must be positive.");
        if (MaxResponseContentLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxResponseContentLength), "Response content length must be positive.");
        if (MaxStateProperties <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStateProperties), "Maximum state properties must be positive.");
        if (MaxStatePropertyKeyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStatePropertyKeyLength), "State-property key length must be positive.");
        if (MaxStatePropertyValueLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStatePropertyValueLength), "State-property value length must be positive.");
        if (MaxTotalCharacters is <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxTotalCharacters), "Total transcript character limit must be positive when specified.");
    }
}
