using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Selects the profile with the highest <see cref="CapabilityProfile.Priority"/>.
/// Among same-priority profiles, selects the cheapest by input price.
/// This is the default selection strategy.
/// </summary>
public sealed class PriorityModelSelectionStrategy : IModelSelectionStrategy
{
    public string Name => "Priority";

    public CapabilityProfile? Select(
        CapabilityRequirements request,
        IReadOnlyList<CapabilityProfile> candidates)
    {
        return candidates
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Capabilities.CostTier ?? (ECostTier)int.MaxValue)
            .FirstOrDefault();
    }
}
