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
}
