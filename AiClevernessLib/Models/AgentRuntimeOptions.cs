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
}
