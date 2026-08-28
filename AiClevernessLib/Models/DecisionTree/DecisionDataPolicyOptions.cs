namespace AiCleverness.Models.DecisionTree;

/// <summary>Default limits used when decision data is represented in a classification prompt.</summary>
public sealed class DecisionDataPolicyOptions
{
    /// <summary>Maximum number of source data items represented in one prompt.</summary>
    public int MaxItems { get; set; } = 50;

    /// <summary>Maximum content length for one represented item.</summary>
    public int MaxContentLengthPerItem { get; set; } = 4_000;

    private int _maxAggregateRepresentationLength = 12_000;

    /// <summary>Maximum canonical representation length across represented source items.</summary>
    public int MaxAggregateRepresentationLength
    {
        get => _maxAggregateRepresentationLength;
        set => _maxAggregateRepresentationLength = value;
    }

    /// <summary>Compatibility alias for <see cref="MaxAggregateRepresentationLength"/>.</summary>
    [Obsolete("Use MaxAggregateRepresentationLength instead.")]
    public int MaxAggregateContentLength
    {
        get => MaxAggregateRepresentationLength;
        set => MaxAggregateRepresentationLength = value;
    }

    /// <summary>Maximum length of an identifier, type, or source display value.</summary>
    public int MaxFieldLength { get; set; } = 256;

    /// <summary>Maximum number of metadata entries retained per represented item.</summary>
    public int MaxMetadataEntries { get; set; } = 20;

    /// <summary>Maximum metadata key length retained per represented item.</summary>
    public int MaxMetadataKeyLength { get; set; } = 256;

    /// <summary>Maximum metadata value length retained per represented item.</summary>
    public int MaxMetadataValueLength { get; set; } = 1_000;

    /// <summary>Optional allow-list of data types. Matching is ordinal and case-sensitive.</summary>
    public IReadOnlySet<string>? IncludedTypes { get; set; }

    /// <summary>Optional allow-list of data sources. Matching is ordinal and case-sensitive.</summary>
    public IReadOnlySet<string>? IncludedSources { get; set; }

    internal void Validate()
    {
        if (MaxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxItems), "Maximum decision data items must be positive.");
        if (MaxContentLengthPerItem <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxContentLengthPerItem), "Per-item decision data content length must be positive.");
        if (MaxAggregateRepresentationLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxAggregateRepresentationLength), "Aggregate decision data representation length must be positive.");
        if (MaxFieldLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxFieldLength), "Decision data field length must be positive.");
        if (MaxMetadataEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataEntries), "Maximum metadata entries must be positive.");
        if (MaxMetadataKeyLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataKeyLength), "Metadata key length must be positive.");
        if (MaxMetadataValueLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxMetadataValueLength), "Metadata value length must be positive.");
    }
}
