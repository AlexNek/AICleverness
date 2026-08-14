using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Returns the first compatible candidate (catalog order = priority).
/// Suitable for single-model-per-profile setups.
/// </summary>
public sealed class DefaultSelectionPolicy : IModelSelectionPolicy
{
    public ValueTask<ModelDefinition?> SelectAsync(
        IReadOnlyList<ModelDefinition> candidates,
        CapabilityRequirements requirements,
        CancellationToken ct = default) =>
        new(candidates.FirstOrDefault());
}
