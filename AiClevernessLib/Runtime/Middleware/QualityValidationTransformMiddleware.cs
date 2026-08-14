using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Filtering;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Middleware;

/// <summary>
/// Pipeline middleware that applies quality gates, validators, and transformers to the result
/// produced by downstream middleware. Handles quality-gate retry logic.
/// </summary>
internal sealed class QualityValidationTransformMiddleware : IAgentPipelineMiddleware
{
    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly IEnumerable<IAgentQualityGate> _qualityGates;

    private readonly IEnumerable<IAgentResultTransformer> _transformers;

    private readonly IEnumerable<IAgentResultValidator> _validators;

    public string Name => "QualityValidationTransform";

    public QualityValidationTransformMiddleware(
        IEnumerable<IAgentQualityGate> qualityGates,
        IEnumerable<IAgentResultValidator> validators,
        IEnumerable<IAgentResultTransformer> transformers,
        IEnumerable<IAgentObserver> observers,
        ILogger? logger = null,
        IExecutionEventPublisher? eventPublisher = null)
    {
        _qualityGates = qualityGates;
        _validators = validators;
        _transformers = transformers;
        _observers = observers;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<AgentResult> InvokeAsync(
        IExecutionContext context,
        AgentPipelineDelegate next)
    {
        var maxQualityRetries =
            context.AgentContext.GetProperty<int?>(AgentPropertyKeys.MaxQualityRetries)
            ?? context.Metadata.Options.DefaultMaxQualityRetries;

        var gateFailures = new List<string>();

        for (var attempt = 0; attempt <= maxQualityRetries; attempt++)
        {
            if (gateFailures.Count > 0)
            {
                context.AgentContext.SetProperty(
                    AgentPropertyKeys.QualityFeedback,
                    string.Join(Environment.NewLine, gateFailures));
                var retryMsg = $"Retrying after quality feedback ({attempt}/{maxQualityRetries}).";
                ExecutionSteps.Add(context, retryMsg);
            }

            var result = await next(context);
            var qualityResult = await ApplyQualityGatesAsync(result, context);

            result = qualityResult.Result;

            if (qualityResult.Approved)
            {
                return await ApplyValidatorsAndTransformersAsync(result, context);
            }

            if (!qualityResult.Retry || attempt >= maxQualityRetries)
            {
                gateFailures.Add(qualityResult.Reason ?? "Quality gate rejected the result.");
                var metadata = new Dictionary<string, object>(result.Metadata)
                                   {
                                       [AgentResultMetadataKeys.QualityGateFailures] =
                                           gateFailures.ToArray(),
                                       [AgentResultMetadataKeys.QualityRetryCount] =
                                           context.State.QualityRetryCount
                                   };

                var failedResult = result with
                                       {
                                           Success = false,
                                           Reasoning = qualityResult.Reason ?? result.Reasoning,
                                           Metadata = metadata
                                       };
                return await ApplyValidatorsAndTransformersAsync(failedResult, context);
            }

            context.State.IncrementQualityRetry();
            gateFailures.Add(qualityResult.Reason ?? "Quality gate requested retry.");
        }

        return new AgentResult(
            false,
            null,
            "Quality retry loop exited unexpectedly.",
            ExecutionSteps.Get(context));
    }

    private async Task<(bool Approved, bool Retry, string? Reason, AgentResult Result)>
        ApplyQualityGatesAsync(
            AgentResult result,
            IExecutionContext context)
    {
        var current = result;
        var agentContext = context.AgentContext;

        foreach (var gate in _qualityGates
                     .Where(g => g.AppliesTo(agentContext))
                     .OrderByDescending(g => g.Priority))
        {
            var gateResult = await gate.EvaluateAsync(
                                 current,
                                 agentContext,
                                 context.CancellationToken);
            if (gateResult.ReplacementResult is not null)
                current = gateResult.ReplacementResult;

            if (gateResult.Approved)
                continue;

            var reason = gateResult.Reason ?? $"Quality gate '{gate.Name}' rejected the result.";
            ExecutionSteps.Add(context, $"Quality gate {gate.Name} rejected result: {reason}");

            await ObserverNotifier.NotifyAllAsync(
                _observers,
                observer => observer.OnGateRejectedAsync(
                    gate,
                    gateResult,
                    context.State.QualityRetryCount,
                    context.CancellationToken),
                _logger,
                context.CancellationToken);

            // Publish quality gate evaluated event.
            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishAsync(
                    new QualityGateEvaluatedBusEvent(
                        context.Metadata.ExecutionId,
                        gate.Name,
                        gateResult.Approved,
                        gateResult.Retry,
                        gateResult.Reason,
                        context.State.QualityRetryCount),
                    context.CancellationToken);
            }

            return (false, gateResult.Retry, reason, current);
        }

        return (true, false, null, current);
    }

    private async Task<AgentResult> ApplyValidatorsAndTransformersAsync(
        AgentResult result,
        IExecutionContext context)
    {
        var current = result;
        var agentContext = context.AgentContext;

        foreach (var validator in _validators)
        {
            if (validator is IAppliesToAgent scoped && !scoped.AppliesTo(agentContext))
                continue;

            var validation = await validator.ValidateAsync(
                                 current,
                                 agentContext,
                                 context.CancellationToken);

            // Publish validation completed event.
            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishAsync(
                    new ValidationCompletedBusEvent(
                        context.Metadata.ExecutionId,
                        validator.Name,
                        validation.IsValid,
                        validation.Error),
                    context.CancellationToken);
            }

            if (validation.IsValid)
                continue;

            var validationMsg = $"Validator {validator.Name} failed: {validation.Error}";
            ExecutionSteps.Add(context, validationMsg);
            current = current with
                          {
                              Success = false, Reasoning = validation.Error ?? current.Reasoning
                          };
            break;
        }

        foreach (var transformer in _transformers.OrderByDescending(t => t.Priority))
        {
            if (transformer is IAppliesToAgent scopedT && !scopedT.AppliesTo(agentContext))
                continue;

            current = await transformer.TransformAsync(
                          current,
                          agentContext,
                          context.CancellationToken);

            // Publish transformation completed event.
            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishAsync(
                    new TransformationCompletedBusEvent(
                        context.Metadata.ExecutionId,
                        transformer.Name),
                    context.CancellationToken);
            }
        }

        return current;
    }
}
