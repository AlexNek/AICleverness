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
    /// <summary>
    /// Names of the tools this run may use. <c>null</c> (the default) means
    /// unrestricted — every registered tool is available. An empty list means
    /// no tools at all.
    /// </summary>
    public IReadOnlyList<string>? AllowedToolNames { get; init; } = AllowedToolNames;

    public IReadOnlyDictionary<string, object> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, object>();
}
