namespace AiCleverness.Models.DecisionTree;

/// <summary>Default decision-tree settings used by dependency injection configuration.</summary>
public sealed class DecisionTreeExecutionOptions
{
    public int DefaultMaxNodeVisits { get; set; } = 20;
    public int DefaultMaxLlmCalls { get; set; } = 10;
    public TimeSpan DefaultMaxElapsedTime { get; set; } = TimeSpan.FromSeconds(120);
    public int DefaultMaxContextTokens { get; set; } = 4000;

    /// <summary>Limits used to select and represent decision data in classification prompts.</summary>
    public DecisionDataPolicyOptions DecisionDataPolicy { get; } = new();

    /// <summary>Limits used when writing decision-specific transcript sections.</summary>
    public DecisionTranscriptPolicyOptions DecisionTranscriptPolicy { get; } = new();

    /// <summary>Primary model identifier used when decision-tree model failover is enabled.</summary>
    public string? Model { get; set; }

    /// <summary>Enables model failover for decision-tree LLM classifications.</summary>
    public bool EnableModelFailover { get; set; }

    /// <summary>
    /// Ordered fallback-only model identifiers. The primary <see cref="Model"/> must not be included.
    /// </summary>
    public IReadOnlyList<string>? ModelFallbackChain { get; set; }

    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }

    /// <summary>Optional absolute directory for decision-tree Markdown transcripts.</summary>
    public string? TranscriptDirectory { get; set; }

    /// <summary>Writes decision-tree transcripts without redaction when enabled. Decision transcript size limits still apply.</summary>
    public bool TranscriptDebug { get; set; }

    /// <summary>Host-provided redactor for normal decision-tree transcripts.</summary>
    public Func<string, string>? TranscriptRedactor { get; set; }
}
