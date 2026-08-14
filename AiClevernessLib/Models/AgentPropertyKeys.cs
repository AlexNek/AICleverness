namespace AiCleverness.Models;

/// <summary>
/// Public constants for runtime property keys used in AgentRequest.Parameters
/// and IAgentContext property access.
/// </summary>
public static class AgentPropertyKeys
{
    /// <summary>LLM completion timeout in seconds.</summary>
    public const string CompletionTimeoutSeconds = "completion_timeout_seconds";

    /// <summary>Maximum quality-gate retry attempts.</summary>
    public const string MaxQualityRetries = "max_quality_retries";

    /// <summary>Maximum LLM tool-loop turns.</summary>
    public const string MaxTurns = "max_turns";

    /// <summary>Model identifier override (legacy — prefer ModelExecutionInfo).</summary>
    public const string Model = "model";

    /// <summary>Full model execution provenance (model, profile, selection reason).</summary>
    public const string ModelExecutionInfo = "model_execution_info";

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
}
