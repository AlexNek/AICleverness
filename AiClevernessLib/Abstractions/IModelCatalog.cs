using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Configuration lookup: profile → candidate model definitions.
/// Takes the full profile, not just its ID, so future versions can
/// consider tenant, region, deployment, feature flags without breaking callers.
/// </summary>
public interface IModelCatalog
{
    ValueTask<IReadOnlyList<ModelDefinition>> GetCandidatesAsync(
        CapabilityProfile profile,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up a model definition by its unique name across all profiles.
    /// Returns null when the name is unknown. Default implementation returns
    /// null; catalogs that can resolve names should override it.
    /// </summary>
    ValueTask<ModelDefinition?> FindByNameAsync(string name, CancellationToken ct = default)
        => new(default(ModelDefinition));
}
