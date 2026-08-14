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

            var selected = await _selectionPolicy.SelectAsync(candidates, requirements, ct);

            if (selected is not null)
            {
                _logger?.LogInformation(
                    "Selected model {ModelName} ({Provider}) via profile '{ProfileId}' (priority {Priority})"
                    +
                    " — attempt {Attempt}/{Total}, fallback={IsFallback}",
                    selected.Name,
                    selected.ProviderKey,
                    profile.Id,
                    profile.Priority,
                    attempts,
                    profiles.Count,
                    attempts > 1);

                return new ModelResolutionResult
                           {
                               Model = selected,
                               Profile = profile,
                               Attempts = attempts,
                               IsFallback = attempts > 1,
                               SelectionReason =
                                   $"Profile '{profile.Id}' (priority {profile.Priority})"
                           };
            }

            _logger?.LogDebug(
                "  [{Attempts}/{Total}] Profile '{ProfileId}': all candidates rejected by policy",
                attempts,
                profiles.Count,
                profile.Id);
        }

        _logger?.LogError(
            "No model found for capabilities: {Capabilities} — {Count} profiles exhausted",
            requirements.Capabilities.CapabilityFlags,
            profiles.Count);
        return null;
    }
}
