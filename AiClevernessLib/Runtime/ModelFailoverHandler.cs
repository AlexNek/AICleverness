using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Encapsulates all failover orchestration: chain resolution, candidate consumption,
/// options rebuild, execution-info updates, and notification emission.
/// Constructed once at the start of each <see cref="LlmToolLoop"/> run.
/// </summary>
internal sealed class ModelFailoverHandler
{
    private readonly Queue<ModelDefinition> _candidates;

    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly CapabilityProfile? _profile;

    public ModelFailoverHandler(
        IAgentContext context,
        AgentRuntimeOptions options,
        IEnumerable<IAgentObserver> observers,
        IExecutionEventPublisher? eventPublisher,
        ILogger? logger)
    {
        _observers = observers;
        _eventPublisher = eventPublisher;
        _logger = logger;

        // Determine if failover is enabled (per-request overrides global).
        var perRequest = context.GetProperty<bool?>(AgentPropertyKeys.EnableModelFailover);
        IsEnabled = perRequest ?? options.EnableModelFailover;

        // Resolve effective chain.
        var explicitChain = context.GetProperty<IReadOnlyList<string>>(
            AgentPropertyKeys.ModelFallbackChain);

        var execInfo = context.GetProperty<ModelExecutionInfo>(
            AgentPropertyKeys.ModelExecutionInfo);
        _profile = execInfo?.Profile;

        // Check if model is pinned (explicit model with no chain).
        var pinnedModel = context.GetProperty<string>(AgentPropertyKeys.Model);
        if (pinnedModel is not null && explicitChain is null
                                    && (execInfo is null
                                        || execInfo.Model.Name == pinnedModel))
        {
            // Pinned model — no fallback regardless of enable flag.
            IsEnabled = false;
        }

        _candidates = new Queue<ModelDefinition>();

        if (!IsEnabled)
        {
            return;
        }

        if (explicitChain is { Count: > 0 })
        {
            // Explicit chain: wrap names into ModelDefinition.
            // Unknown names are skipped (validated downstream if catalog available).
            foreach (var name in explicitChain)
            {
                _candidates.Enqueue(new ModelDefinition { Name = name, ProviderKey = "explicit" });
            }

            _logger?.LogDebug(
                "Model failover enabled with explicit chain of {Count} candidates",
                _candidates.Count);
        }
        else if (execInfo is not null)
        {
            // Resolution-based chain from ModelResolutionResult.Fallbacks
            // (stored in ModelExecutionInfo indirectly via the context).
            var resolutionResult = context.GetProperty<ModelResolutionResult>(
                "model_resolution_result");
            if (resolutionResult?.Fallbacks is { Count: > 0 } fallbacks)
            {
                foreach (var fb in fallbacks)
                {
                    _candidates.Enqueue(fb);
                }

                _logger?.LogDebug(
                    "Model failover enabled with {Count} resolution-based fallbacks",
                    _candidates.Count);
            }
        }

        // If the chain is empty, failover has nothing to do.
        if (_candidates.Count == 0)
        {
            IsEnabled = false;
        }
    }

    /// <summary>Whether there is at least one more candidate in the chain.</summary>
    public bool HasNextCandidate => _candidates.Count > 0;

    /// <summary>Whether failover is enabled and the chain has candidates.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Builds a new <see cref="LlmCompletionOptions"/> with the next model name,
    /// preserving temperature and max tokens from the original.
    /// </summary>
    public LlmCompletionOptions BuildOptions(LlmCompletionOptions original, ModelDefinition next)
    {
        return new LlmCompletionOptions(original.Temperature, original.MaxTokens, next.Name);
    }

    /// <summary>
    /// Builds an updated <see cref="ModelExecutionInfo"/> reflecting the failover.
    /// </summary>
    public ModelExecutionInfo BuildExecutionInfo(
        ModelExecutionInfo current,
        ModelDefinition next,
        string reason)
    {
        return current with
        {
            Model = next,
            Attempt = current.Attempt + 1,
            IsFallback = true,
            RemainingFallbacks = _candidates.Count,
            SelectionReason = $"runtime failover after {reason}"
        };
    }

    /// <summary>
    /// Consumes and returns the next candidate from the chain.
    /// </summary>
    public ModelDefinition ConsumeNext()
    {
        return _candidates.Dequeue();
    }

    /// <summary>
    /// Emits all failover notifications: observer, streaming event, and bus event.
    /// </summary>
    public async Task NotifySwitchAsync(
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
