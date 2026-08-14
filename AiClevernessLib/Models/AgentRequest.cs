namespace AiCleverness.Models;

/// <summary>
/// Input to an agent run.
/// </summary>
public sealed record AgentRequest(
    string Goal,
    IReadOnlyList<string>? AllowedToolNames = null,
    IReadOnlyDictionary<string, object>? Parameters = null,
    string? AgentName = null,
    CapabilityRequirements? CapabilityRequirements = null)
{
    public IReadOnlyList<string> AllowedToolNames { get; init; } =
        AllowedToolNames ?? Array.Empty<string>();

    public IReadOnlyDictionary<string, object> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, object>();
}
