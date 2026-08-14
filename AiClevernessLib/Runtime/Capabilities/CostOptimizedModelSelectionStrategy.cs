using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Selects the cheapest profile from matching candidates.
/// Among same-cost profiles, prefers the highest priority.
/// </summary>
public sealed class CostOptimizedModelSelectionStrategy : IModelSelectionStrategy
{
    public string Name => "CostOptimized";

    public CapabilityProfile? Select(
        CapabilityRequirements request,
        IReadOnlyList<CapabilityProfile> candidates)
    {
        return candidates
            .OrderBy(c => c.Capabilities.CostTier ?? (ECostTier)int.MaxValue)
            .ThenByDescending(c => c.Priority)
            .FirstOrDefault();
    }
}
