using AiCleverness.Runtime.Transcript;

namespace AiCleverness.Models;

/// <summary>
/// Default runtime limits and behavior. Request parameters can override these values per run.
/// </summary>
public sealed class AgentRuntimeOptions
{
    public int DefaultCompletionTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Default idle silence threshold in seconds between meaningful chunks during streaming.
    /// When the LLM client supports streaming, a stream is considered stalled if no
    /// meaningful chunk arrives within this duration. Default is 30 seconds.
    /// </summary>
    public int DefaultIdleTimeoutSeconds { get; set; } = 30;

    public int DefaultMaxQualityRetries { get; set; } = 1;

    public int DefaultMaxTurns { get; set; } = 8;

    public string DefaultSystemPrompt { get; set; } =
        "You are a helpful assistant with access to tools. Use tools when needed.";

    public float DefaultTemperature { get; set; } = 0.1f;

    public bool DefaultToolLoggingEnabled { get; set; } = true;

    public int DefaultToolMaxRetries { get; set; }

    public bool DefaultToolMetricsEnabled { get; set; } = true;

    public int? DefaultToolTimeoutSeconds { get; set; }

    /// <summary>
    /// When <c>true</c>, the tool loop will fail over to the next candidate model
    /// in the chain on transient failures (e.g. per-turn timeout). Default is <c>false</c>.
    /// Can be overridden per-request via <see cref="AgentPropertyKeys.EnableModelFailover"/>.
    /// </summary>
    public bool EnableModelFailover { get; set; }

    /// <summary>
    /// Host-provided redactor used for opt-in Markdown transcripts. The delegate
    /// must return content safe to persist and must be thread-safe when the runtime
    /// is shared across concurrent executions. A missing redactor disables normal
    /// transcript persistence; explicit debug mode may bypass it.
    /// </summary>
    public Func<string, string>? TranscriptRedactor { get; set; }

    /// <summary>
    /// Creates a new transcript builder for each enabled execution. The returned builder must not be shared between executions.
    /// </summary>
    public Func<ITranscriptBuilder>? TranscriptBuilderFactory { get; set; }

    /// <summary>
    /// Creates a new transcript sink for each enabled execution. The argument is the intended logical transcript path.
    /// </summary>
    public Func<string, ITranscriptSink>? TranscriptSinkFactory { get; set; }
}
