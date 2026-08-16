using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Capabilities;

/// <summary>
/// Default orchestrator: resolver → catalog → policy.
/// Receives pre-filtered profiles from <see cref="ICapabilityResolver"/>,
/// iterates them, calls catalog + policy.
/// </summary>
public sealed class DefaultModelManager : IModelManager
{
    private readonly ICapabilityResolver _capabilityResolver;

    private readonly IModelCatalog _catalog;

    private readonly ILogger<DefaultModelManager>? _logger;

    private readonly IModelSelectionPolicy _selectionPolicy;

    public DefaultModelManager(
        ICapabilityResolver capabilityResolver,
        IModelCatalog catalog,
        IModelSelectionPolicy selectionPolicy,
        ILogger<DefaultModelManager>? logger = null)
    {
        _capabilityResolver = capabilityResolver
                              ?? throw new ArgumentNullException(nameof(capabilityResolver));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _selectionPolicy =
            selectionPolicy ?? throw new ArgumentNullException(nameof(selectionPolicy));
        _logger = logger;
    }

    public async ValueTask<ModelResolutionResult?> ResolveAsync(
        CapabilityRequirements requirements,
        CancellationToken ct = default)
    {
        var profiles = _capabilityResolver.GetProfiles()
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.Priority)
            .ToList();

        _logger?.LogDebug(
            "Resolving model for capabilities: {Capabilities} — {Count} profiles available",
            requirements.Capabilities.CapabilityFlags,
            profiles.Count);

        var attempts = 0;
        var fallbacks = new List<ModelDefinition>();
        ModelDefinition? selected = null;
        CapabilityProfile? selectedProfile = null;
        int selectedAttempts = 0;

        foreach (var profile in profiles)
        {
            attempts++;

            var candidates = await _catalog.GetCandidatesAsync(profile, ct);

            if (candidates.Count == 0)
            {
                _logger?.LogDebug(
                    "  [{Attempts}/{Total}] Profile '{ProfileId}': no model mapping",
                    attempts,
                    profiles.Count,
                    profile.Id);
                continue;
            }

            if (selected is null)
            {
                // First profile with candidates: select primary + collect runners-up.
                var ranked = await GetRankedCandidatesAsync(candidates, requirements, ct);
                if (ranked.Count > 0)
                {
                    selected = ranked[0];
                    selectedProfile = profile;
                    selectedAttempts = attempts;

                    // Runners-up from the same profile become fallbacks.
                    for (var i = 1; i < ranked.Count; i++)
                    {
                        fallbacks.Add(ranked[i]);
                    }

                    continue;
                }

                _logger?.LogDebug(
                    "  [{Attempts}/{Total}] Profile '{ProfileId}': all candidates rejected by policy",
                    attempts,
                    profiles.Count,
                    profile.Id);
            }
            else
            {
                // Lower-priority profile: best candidate becomes a fallback.
                var best = await _selectionPolicy.SelectAsync(candidates, requirements, ct);
                if (best is not null)
                {
                    fallbacks.Add(best);
                }
            }
        }

        if (selected is not null && selectedProfile is not null)
        {
            _logger?.LogInformation(
                "Selected model {ModelName} ({Provider}) via profile '{ProfileId}' (priority {Priority})"
                + " — attempt {Attempt}/{Total}, fallback={IsFallback}, chain length={ChainLength}",
                selected.Name,
                selected.ProviderKey,
                selectedProfile.Id,
                selectedProfile.Priority,
                selectedAttempts,
                profiles.Count,
                selectedAttempts > 1,
                fallbacks.Count);

            return new ModelResolutionResult
            {
                Model = selected,
                Profile = selectedProfile,
                Attempts = selectedAttempts,
                IsFallback = selectedAttempts > 1,
                Fallbacks = fallbacks.AsReadOnly(),
                SelectionReason =
                    $"Profile '{selectedProfile.Id}' (priority {selectedProfile.Priority})"
            };
        }

        _logger?.LogError(
            "No model found for capabilities: {Capabilities} — {Count} profiles exhausted",
            requirements.Capabilities.CapabilityFlags,
            profiles.Count);
        return null;
    }

    /// <summary>
    /// Returns all candidates that pass the selection policy, ranked in policy order.
    /// </summary>
    private async ValueTask<IReadOnlyList<ModelDefinition>> GetRankedCandidatesAsync(
        IReadOnlyList<ModelDefinition> candidates,
        CapabilityRequirements requirements,
        CancellationToken ct)
    {
        // Ask the policy for the primary pick, then iteratively get the next best
        // by excluding already-selected candidates.
        var ranked = new List<ModelDefinition>();
        var remaining = new List<ModelDefinition>(candidates);

        while (remaining.Count > 0)
        {
            var pick = await _selectionPolicy.SelectAsync(remaining, requirements, ct);
            if (pick is null)
            {
                break;
            }

            ranked.Add(pick);
            remaining.Remove(pick);
        }

        return ranked;
    }
}
