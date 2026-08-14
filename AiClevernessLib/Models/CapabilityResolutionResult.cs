namespace AiCleverness.Models;

/// <summary>
/// Result of capability resolution.
/// </summary>
public sealed record CapabilityResolutionResult(
    bool Resolved,
    CapabilityProfile? SelectedProfile = null,
    string? Reason = null,
    IReadOnlyList<CapabilityProfile>? Candidates = null)
{
    public IReadOnlyList<CapabilityProfile> Candidates { get; init; } =
        Candidates ?? Array.Empty<CapabilityProfile>();

    /// <summary>Creates a failed resolution.</summary>
    public static CapabilityResolutionResult Failed(
        string reason,
        IReadOnlyList<CapabilityProfile>? candidates = null) =>
        new(false, null, reason, candidates);

    /// <summary>Creates a successful resolution.</summary>
    public static CapabilityResolutionResult Success(
        CapabilityProfile profile,
        IReadOnlyList<CapabilityProfile>? candidates = null) =>
        new(true, profile, null, candidates);
}
