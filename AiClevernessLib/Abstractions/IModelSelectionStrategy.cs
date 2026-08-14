using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Strategy for selecting the best model from a set of matching candidates.
/// </summary>
public interface IModelSelectionStrategy
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Selects the best profile from the candidates given the request.
    /// </summary>
    CapabilityProfile? Select(
        CapabilityRequirements request,
        IReadOnlyList<CapabilityProfile> candidates);
}
