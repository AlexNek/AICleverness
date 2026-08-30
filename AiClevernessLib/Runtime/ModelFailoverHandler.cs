using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Encapsulates all failover orchestration: chain resolution, candidate consumption,
/// options rebuild, execution-info updates, and notification emission.
/// Created once per <see cref="LlmToolLoop"/> run via <see cref="CreateAsync"/>.
/// </summary>
internal sealed class ModelFailoverHandler
{
    private readonly Queue<ModelDefinition> _candidates;

    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentObserver> _observers;

    private ModelExecutionInfo? _execInfo;

    private ModelFailoverHandler(
        IEnumerable<IAgentObserver> observers,
        IExecutionEventPublisher? eventPublisher,
        ILogger? logger,
        Queue<ModelDefinition> candidates,
        ModelExecutionInfo? execInfo,
        bool isEnabled)
    {
        _observers = observers;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _candidates = candidates;
        _execInfo = execInfo;
        IsEnabled = isEnabled;
    }

    /// <summary>1-based attempt number within the failover chain.</summary>
    public int Attempt { get; private set; } = 1;

    /// <summary>Whether failover is enabled and the chain has candidates.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Whether the current attempt runs on a fallback model.</summary>
    public bool IsOnFallback => Attempt > 1;

    /// <summary>
    /// Creates a handler with the effective chain resolved from request parameters
    /// and capability-resolution provenance. Explicit chain names are validated
    /// against the catalog when one is available.
    /// </summary>
    public static async Task<ModelFailoverHandler> CreateAsync(
        IAgentContext context,
        AgentRuntimeOptions options,
        IEnumerable<IAgentObserver> observers,
        IExecutionEventPublisher? eventPublisher,
        ILogger? logger,
        IModelCatalog? catalog = null,
        CancellationToken cancellationToken = default)
    {
        // Determine if failover is enabled (per-request overrides global).
        var perRequest = context.GetProperty<bool?>(AgentPropertyKeys.EnableModelFailover);
        var isEnabled = perRequest ?? options.EnableModelFailover;

        // Resolve effective chain.
        var explicitChain = context.GetProperty<IReadOnlyList<string>>(
            AgentPropertyKeys.ModelFallbackChain);

        var execInfo = context.GetProperty<ModelExecutionInfo>(
            AgentPropertyKeys.ModelExecutionInfo);

        // Pinned model: the caller set a model explicitly without capability
        // resolution (no provenance) and without a fallback chain. When
        // provenance exists, resolution owns the model choice and its fallback
        // chain decides failover — the pin does not apply.
        var pinnedModel = context.GetProperty<string>(AgentPropertyKeys.Model);
        if (pinnedModel is not null && explicitChain is null && execInfo is null)
        {
            // Pinned model — no fallback regardless of enable flag.
            isEnabled = false;
        }

        var candidates = new Queue<ModelDefinition>();

        if (isEnabled)
        {
            if (explicitChain is { Count: > 0 })
            {
                // Explicit chain: resolve names against the catalog when
                // available (unknown names are skipped with a warning),
                // excluding the active model and duplicates so failover never
                // retries the current model.
                var activeModel = execInfo?.Model.Name ?? pinnedModel;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var name in explicitChain)
                {
                    ModelDefinition definition;
                    if (catalog is null)
                    {
                        // No catalog — accept the name as-is.
                        definition = new ModelDefinition
                                         {
                                             Name = name, ProviderKey = "explicit"
                                         };
                    }
                    else
                    {
                        var resolved = await catalog.FindByNameAsync(name, cancellationToken);
                        if (resolved is null)
                        {
                            logger?.LogWarning(
                                "Model failover: fallback model '{ModelName}' not found in catalog — skipped",
                                name);
                            continue;
                        }

                        definition = resolved;
                    }

                    if (activeModel is not null
                        && string.Equals(definition.Name, activeModel, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(
                            "Model failover: fallback '{ModelName}' is the active model — skipped",
                            definition.Name);
                        continue;
                    }

                    if (!seen.Add(definition.Name))
                    {
                        logger?.LogDebug(
                            "Model failover: fallback '{ModelName}' appears more than once — skipped",
                            definition.Name);
                        continue;
                    }

                    candidates.Enqueue(definition);
                }

                logger?.LogDebug(
                    "Model failover enabled with explicit chain of {Count} candidates",
                    candidates.Count);
            }
            else if (execInfo is not null)
            {
                // Resolution-based chain from ModelResolutionResult.Fallbacks.
                var resolutionResult = context.GetProperty<ModelResolutionResult>(
                    AgentPropertyKeys.ModelResolutionResult);
                if (resolutionResult?.Fallbacks is { Count: > 0 } fallbacks)
                {
                    foreach (var fallback in fallbacks)
                    {
                        candidates.Enqueue(fallback);
                    }

                    logger?.LogDebug(
                        "Model failover enabled with {Count} resolution-based fallbacks",
                        candidates.Count);
                }
            }
        }

        // If the chain is empty, failover has nothing to do.
        if (candidates.Count == 0)
        {
            isEnabled = false;
        }

        return new ModelFailoverHandler(
            observers,
            eventPublisher,
            logger,
            candidates,
            execInfo,
            isEnabled);
    }

    /// <summary>
    /// Performs the full failover switch for a transient failure: consumes the
    /// next candidate, reports the switch step, emits the transient failure event
    /// and all switch notifications, updates context provenance, and rebuilds
    /// completion options. Returns the new options, or null when no candidate
    /// remains.
    /// </summary>
    public async Task<LlmCompletionOptions?> TryFailoverAsync(
        string executionId,
        int turn,
        LlmCompletionOptions currentOptions,
        IAgentContext context,
        string failureError,
        string failureVerb,
        string reason,
        LlmProviderFailureMetadata? providerFailure,
        List<string> steps,
        Action<string> report,
        Action<AgentEvent>? emit,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || _candidates.Count == 0)
        {
            return null;
        }

        var fromModel = currentOptions.Model ?? "unknown";
        Attempt++;
        var nextCandidate = _candidates.Dequeue();
        if (context.GetProperty<IReadOnlyList<string>>(AgentPropertyKeys.ModelFallbackChain) is not null)
        {
            context.SetProperty<IReadOnlyList<string>>(
                AgentPropertyKeys.ModelFallbackChain,
                _candidates.Select(candidate => candidate.Name).ToArray());
        }

        var switchMessage =
            $"Model '{fromModel}' {failureVerb}; switching to '{nextCandidate.Name}' (conversation continues)";
        _logger?.LogWarning("Agent: {Message}", switchMessage);
        steps.Add(switchMessage);
        report(switchMessage);

        // Transient failure event first, then all switch notifications.
        emit?.Invoke(new FailureEvent
                         {
                             ExecutionId = executionId,
                             Error = failureError,
                             Phase = LlmFailurePhases.LlmCompletion,
                             IsTransient = true,
                             ProviderFailure = providerFailure
                         });

        await NotifySwitchAsync(
            executionId,
            fromModel,
            nextCandidate.Name,
            reason,
            turn,
            emit,
            cancellationToken);

        // Update context provenance.
        if (_execInfo is not null)
        {
            _execInfo = _execInfo with
                        {
                            Model = nextCandidate,
                            Attempt = _execInfo.Attempt + 1,
                            IsFallback = true,
                            RemainingFallbacks = _candidates.Count,
                            SelectionReason = $"runtime failover after {reason}"
                        };
            context.SetProperty(AgentPropertyKeys.ModelExecutionInfo, _execInfo);
        }

        context.SetProperty(AgentPropertyKeys.Model, nextCandidate.Name);

        // Rebuild options with the new model.
        return new LlmCompletionOptions(
            currentOptions.Temperature,
            currentOptions.MaxTokens,
            nextCandidate.Name);
    }

    /// <summary>
    /// Emits all failover notifications: observer, streaming event, and bus event.
    /// </summary>
    private async Task NotifySwitchAsync(
        string executionId,
        string fromModel,
        string toModel,
        string reason,
        int turn,
        Action<AgentEvent>? emit,
        CancellationToken cancellationToken)
    {
        // Observer notification.
        await ObserverNotifier.NotifyAllAsync(
            _observers,
            observer => observer.OnModelSwitchedAsync(fromModel, toModel, reason, cancellationToken),
            _logger,
            cancellationToken);

        // Streaming event.
        emit?.Invoke(new ModelSwitchedAgentEvent
                         {
                             ExecutionId = executionId,
                             From = fromModel,
                             To = toModel,
                             Reason = reason,
                             Turn = turn
                         });

        // Bus event.
        if (_eventPublisher is not null)
        {
            await _eventPublisher.PublishAsync(
                new ModelSwitchedBusEvent(executionId, fromModel, toModel, reason, turn),
                cancellationToken);
        }
    }
}
