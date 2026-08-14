namespace AiCleverness.Models;

/// <summary>
/// Runtime policy used by the tool executor.
/// </summary>
public sealed record ToolExecutionPolicy(
    int MaxRetries = 0,
    TimeSpan? Timeout = null,
    bool LogEnabled = true,
    bool MetricsEnabled = true);
