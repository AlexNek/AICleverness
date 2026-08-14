using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Resolves capability requirements to a specific model/provider profile.
/// Used by the runtime to select the best LLM endpoint for an execution.
/// </summary>
public interface ICapabilityResolver
{
    /// <summary>
    /// Gets all available profiles.
    /// </summary>
    IReadOnlyList<CapabilityProfile> GetProfiles();

    /// <summary>
    /// Resolves a capability request to the best matching profile.
    /// </summary>
    Task<CapabilityResolutionResult> ResolveAsync(
        CapabilityRequirements request,
        CancellationToken cancellationToken = default);
}
