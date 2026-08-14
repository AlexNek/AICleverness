using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Decides which model to use among candidates.
/// Implementations: DefaultSelectionPolicy, CheapestSelectionPolicy, FastestSelectionPolicy,
/// LoadBalancedSelectionPolicy, etc.
/// </summary>
public interface IModelSelectionPolicy
{
    ValueTask<ModelDefinition?> SelectAsync(
        IReadOnlyList<ModelDefinition> candidates,
        CapabilityRequirements requirements,
        CancellationToken ct = default);
}
