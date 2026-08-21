namespace AiCleverness.Models;

/// <summary>
/// Public constants for keys used in <see cref="AgentResult.Metadata"/>.
/// </summary>
public static class AgentResultMetadataKeys
{
    /// <summary>Array of quality gate failure messages.</summary>
    public const string QualityGateFailures = "quality_gate_failures";

    /// <summary>Total number of quality-gate retry attempts.</summary>
    public const string QualityRetryCount = "quality_retry_count";

    /// <summary>Absolute path of a completed Markdown execution transcript.</summary>
    public const string MarkdownTranscriptPath = "markdown_transcript_path";

    /// <summary>Persistence status of the Markdown execution transcript.</summary>
    public const string MarkdownTranscriptStatus = "markdown_transcript_status";
}
