namespace AiCleverness.Models;

/// <summary>
/// Runtime health status snapshot.
/// </summary>
public sealed record RuntimeHealthStatus
{
    /// <summary>Number of currently active executions.</summary>
    public int ActiveExecutions { get; init; }

    /// <summary>UTC timestamp when the health check was performed.</summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Individual component health entries.</summary>
    public IReadOnlyList<ComponentHealthEntry> Components { get; init; } =
        Array.Empty<ComponentHealthEntry>();

    /// <summary>Total execution duration of the health check.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether all components are healthy.</summary>
    public bool IsHealthy => Status == HealthState.Healthy;

    /// <summary>Whether the runtime is in shutdown state.</summary>
    public bool IsShuttingDown { get; init; }

    /// <summary>Arbitrary properties for external consumers.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Overall health status.</summary>
    public HealthState Status { get; init; } = HealthState.Healthy;
}

/// <summary>
/// Health status of an individual runtime component.
/// </summary>
public sealed record ComponentHealthEntry(
    string Name,
    HealthState Status,
    string? Description = null,
    TimeSpan? Duration = null,
    IReadOnlyDictionary<string, string>? Properties = null);

/// <summary>
/// Health state enum aligned with ASP.NET Core HealthChecks conventions.
/// </summary>
public enum HealthState
{
    /// <summary>Component is healthy.</summary>
    Healthy,

    /// <summary>Component is degraded but functional.</summary>
    Degraded,

    /// <summary>Component is unhealthy.</summary>
    Unhealthy
}
