namespace AiCleverness.Models;

/// <summary>Runtime health status snapshot.</summary>
public sealed record RuntimeHealthStatus
{
    public int ActiveExecutions { get; init; }
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ComponentHealthEntry> Components { get; init; } = Array.Empty<ComponentHealthEntry>();
    public TimeSpan Duration { get; init; }
    public bool IsHealthy => Status == HealthState.Healthy;
    public bool IsShuttingDown { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    public HealthState Status { get; init; } = HealthState.Healthy;
}
