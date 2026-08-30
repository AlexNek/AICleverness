namespace AiCleverness.Models;

/// <summary>Health status of an individual runtime component.</summary>
public sealed record ComponentHealthEntry(
    string Name,
    HealthState Status,
    string? Description = null,
    TimeSpan? Duration = null,
    IReadOnlyDictionary<string, string>? Properties = null);
