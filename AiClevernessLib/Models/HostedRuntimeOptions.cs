namespace AiCleverness.Models;

/// <summary>
/// Options for configuring the hosted runtime service.
/// </summary>
public sealed class HostedRuntimeOptions
{
    /// <summary>
    /// When <c>true</c>, the hosted runtime will automatically checkpoint running
    /// executions before shutdown. Requires <see cref="AiCleverness.Abstractions.ICheckpointStore"/>
    /// to be registered.
    /// </summary>
    public bool AutoCheckpointOnShutdown { get; set; }

    /// <summary>
    /// Maximum number of concurrent executions the hosted runtime will accept.
    /// Default is 10. Set to 0 for unlimited.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 10;

    /// <summary>
    /// Grace period for running executions to complete during shutdown.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}
