using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Default catalog that maps profile IDs to candidate model definitions.
/// Catalog stores complete metadata — no brittle name parsing.
/// </summary>
public sealed class DefaultModelCatalog : IModelCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModelDefinition>> _mapping;

    public DefaultModelCatalog(IReadOnlyDictionary<string, IReadOnlyList<ModelDefinition>> mapping)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public ValueTask<IReadOnlyList<ModelDefinition>> GetCandidatesAsync(
        CapabilityProfile profile,
        CancellationToken ct = default)
    {
        if (_mapping.TryGetValue(profile.Id, out var candidates) && candidates.Count > 0)
            return new ValueTask<IReadOnlyList<ModelDefinition>>(candidates);

        return new ValueTask<IReadOnlyList<ModelDefinition>>(Array.Empty<ModelDefinition>());
    }
}
