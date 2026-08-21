namespace AiCleverness.Models;

/// <summary>
/// Public constants for runtime property keys used in AgentRequest.Parameters
/// and IAgentContext property access.
/// </summary>
public static class AgentPropertyKeys
{
    /// <summary>LLM completion timeout in seconds.</summary>
    public const string CompletionTimeoutSeconds = "completion_timeout_seconds";

    /// <summary>
    /// Idle silence threshold in seconds between meaningful chunks during streaming.
    /// Only used when the LLM client supports streaming. If no meaningful chunk is
    /// received within this duration, the stream is considered stalled.
    /// </summary>
    public const string IdleTimeoutSeconds = "idle_timeout_seconds";

    /// <summary>
    /// Per-request override to enable/disable model failover (bool).
    /// Overrides <see cref="AgentRuntimeOptions.EnableModelFailover"/>.
    /// </summary>
    public const string EnableModelFailover = "enable_model_failover";

    /// <summary>Maximum quality-gate retry attempts.</summary>
    public const string MaxQualityRetries = "max_quality_retries";

    /// <summary>Maximum LLM tool-loop turns.</summary>
    public const string MaxTurns = "max_turns";

    /// <summary>Model identifier override (legacy — prefer ModelExecutionInfo).</summary>
    public const string Model = "model";

    /// <summary>Full model execution provenance (model, profile, selection reason).</summary>
    public const string ModelExecutionInfo = "model_execution_info";

    /// <summary>
    /// Ordered fallback model names for runtime failover (<see cref="IReadOnlyList{String}"/>).
    /// When present, takes precedence over the capability-resolved chain.
    /// Model names that cannot be found in the catalog are skipped with a warning.
    /// </summary>
    public const string ModelFallbackChain = "model_fallback_chain";

    /// <summary>
    /// Full <see cref="ModelResolutionResult"/> produced by capability resolution.
    /// Stored by the runtime so the failover handler can access the fallback chain.
    /// </summary>
    public const string ModelResolutionResult = "model_resolution_result";

    /// <summary>Quality feedback text passed into the next LLM attempt.</summary>
    public const string QualityFeedback = "quality_feedback";

    /// <summary>Override for the default system prompt.</summary>
    public const string SystemPrompt = "system_prompt";

    /// <summary>LLM temperature override.</summary>
    public const string Temperature = "temperature";

    /// <summary>Whether tool execution logging is enabled.</summary>
    public const string ToolLoggingEnabled = "tool_logging_enabled";

    /// <summary>Tool execution max retries.</summary>
    public const string ToolMaxRetries = "tool_max_retries";

    /// <summary>Whether tool execution metrics are enabled.</summary>
    public const string ToolMetricsEnabled = "tool_metrics_enabled";

    /// <summary>Tool execution timeout in seconds.</summary>
    public const string ToolTimeoutSeconds = "tool_timeout_seconds";

    /// <summary>
    /// Absolute directory for the opt-in Markdown execution transcript.
    /// </summary>
    public const string MarkdownTranscriptDirectory = "markdown_transcript_directory";

    /// <summary>Explicitly enables unredacted transcript debug mode (bool).</summary>
    public const string MarkdownTranscriptDebug = "markdown_transcript_debug";
}

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
