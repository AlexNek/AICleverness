namespace AiCleverness.Models;

/// <summary>
/// Default runtime limits and behavior. Request parameters can override these values per run.
/// </summary>
public sealed class AgentRuntimeOptions
{
    public int DefaultCompletionTimeoutSeconds { get; set; } = 60;

    public int DefaultMaxQualityRetries { get; set; } = 1;

    public int DefaultMaxTurns { get; set; } = 8;

    public string DefaultSystemPrompt { get; set; } =
        "You are a helpful assistant with access to tools. Use tools when needed.";

    public float DefaultTemperature { get; set; } = 0.1f;

    public bool DefaultToolLoggingEnabled { get; set; } = true;

    public int DefaultToolMaxRetries { get; set; }

    public bool DefaultToolMetricsEnabled { get; set; } = true;

    public int? DefaultToolTimeoutSeconds { get; set; }
}
