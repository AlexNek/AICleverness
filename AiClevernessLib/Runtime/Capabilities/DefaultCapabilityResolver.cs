using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Default implementation of <see cref="ICapabilityResolver"/>.
/// Filters profiles against the request requirements, then delegates selection
/// to an <see cref="IModelSelectionStrategy"/>. Supports fallback resolution.
/// </summary>
public sealed class DefaultCapabilityResolver : ICapabilityResolver
{
    private readonly CapabilityProfile? _fallbackProfile;

    private readonly List<CapabilityProfile> _profiles = new();

    private readonly IModelSelectionStrategy _selectionStrategy;

    public DefaultCapabilityResolver(
        IModelSelectionStrategy? selectionStrategy = null,
        CapabilityProfile? fallbackProfile = null)
    {
        _selectionStrategy = selectionStrategy ?? new PriorityModelSelectionStrategy();
        _fallbackProfile = fallbackProfile;
    }

    public DefaultCapabilityResolver(
        IEnumerable<CapabilityProfile> profiles,
        IModelSelectionStrategy? selectionStrategy = null,
        CapabilityProfile? fallbackProfile = null)
        : this(selectionStrategy, fallbackProfile)
    {
        _profiles.AddRange(profiles);
    }

    /// <summary>Adds a profile to the resolver.</summary>
    public void AddProfile(CapabilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profiles.Add(profile);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CapabilityProfile> GetProfiles() => _profiles.AsReadOnly();

    /// <inheritdoc/>
    public Task<CapabilityResolutionResult> ResolveAsync(
        CapabilityRequirements request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = _profiles
            .Where(p => p.IsAvailable)
            .Where(p => Matches(p, request))
            .ToList();

        if (candidates.Count == 0)
        {
            if (_fallbackProfile is not null && _fallbackProfile.IsAvailable)
            {
                return Task.FromResult(
                    CapabilityResolutionResult.Success(
                        _fallbackProfile,
                            [_fallbackProfile]));
            }

            return Task.FromResult(
                CapabilityResolutionResult.Failed(
                    "No available profile matches the requested capabilities.",
                    _profiles.Where(p => p.IsAvailable).ToList()));
        }

        var selected = _selectionStrategy.Select(request, candidates);
        if (selected is null)
        {
            return Task.FromResult(
                CapabilityResolutionResult.Failed(
                    "Selection strategy returned no result.",
                    candidates));
        }

        return Task.FromResult(CapabilityResolutionResult.Success(selected, candidates));
    }

    private static bool Matches(CapabilityProfile profile, CapabilityRequirements requirements)
    {
        var req = requirements.Capabilities;
        var cap = profile.Capabilities;

        // Each required flag must be present in profile (or profile null = assume capable).
        if (req.CapabilityFlags.HasValue && cap.CapabilityFlags.HasValue &&
            (req.CapabilityFlags.Value & ~cap.CapabilityFlags.Value) != 0)
            return false;

        // Technical constraints (ranges, not flags)

        // Context window: profile's actual >= requirement's minimum
        if (req.MinContextWindow.HasValue && cap.MinContextWindow.HasValue &&
            cap.MinContextWindow.Value < req.MinContextWindow.Value)
            return false;

        // Latency: profile's typical <= requirement's maximum acceptable
        if (req.MaxLatencyMs.HasValue && cap.MaxLatencyMs.HasValue &&
            cap.MaxLatencyMs.Value > req.MaxLatencyMs.Value)
            return false;

        // Quality tier: profile's tier >= requirement's minimum tier
        if (req.QualityTier.HasValue && cap.QualityTier.HasValue &&
            cap.QualityTier.Value < req.QualityTier.Value)
            return false;

        // Cost tier: profile's tier <= requirement's maximum tier (budget intent)
        if (req.CostTier.HasValue && cap.CostTier.HasValue &&
            cap.CostTier.Value > req.CostTier.Value)
            return false;

        return true;
    }
}
