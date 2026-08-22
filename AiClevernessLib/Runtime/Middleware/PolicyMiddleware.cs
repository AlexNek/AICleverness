using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Transcript;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Middleware;

/// <summary>
/// Pipeline middleware that evaluates registered policies before execution.
/// If any policy recommends "block", the pipeline is short-circuited.
/// </summary>
internal sealed class PolicyMiddleware : IAgentPipelineMiddleware
{
    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly IEnumerable<IAgentPolicy> _policies;

    public string Name => "PolicyEvaluation";

    public PolicyMiddleware(
        IEnumerable<IAgentPolicy> policies,
        IEnumerable<IAgentObserver> observers,
        ILogger? logger = null,
        IExecutionEventPublisher? eventPublisher = null)
    {
        _policies = policies;
        _observers = observers;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<AgentResult> InvokeAsync(
        IExecutionContext context,
        AgentPipelineDelegate next)
    {
        var agentContext = context.AgentContext;
        var applicablePolicies = _policies
            .Where(p => p.AppliesTo(agentContext))
            .OrderByDescending(p => p.Priority)
            .ToList();

        foreach (var policy in applicablePolicies)
        {
            _logger?.LogDebug("Evaluating policy {PolicyName} before run", policy.Name);
            var result = await policy.EvaluateAsync(agentContext, context.CancellationToken);

            if (result.Applied && string.Equals(
                    result.Recommendation,
                    "block",
                    StringComparison.OrdinalIgnoreCase))
            {
                var reason = result.Reasoning ?? $"Policy '{policy.Name}' disallowed execution.";
                context.State.MarkCompleted(ExecutionStatus.Blocked);
                context.State.StatusDetail = $"Blocked by policy {policy.Name}: {reason}";
                ExecutionSteps.Add(context, $"Blocked by policy {policy.Name}: {reason}");
                context.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript)
                    ?.AppendStatus($"Blocked by policy {policy.Name}", reason);

                // Emit streaming event when running under the streaming entry point.
                var emit =
                    context.Items.Get<Action<AgentEvent>>(ExecutionItemKeys.EventEmitter);
                emit?.Invoke(new PolicyBlockedAgentEvent
                                 {
                                     ExecutionId = context.Metadata.ExecutionId,
                                     PolicyName = policy.Name,
                                     Reason = reason
                                 });

                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnPolicyBlockedAsync(
                        policy,
                        result,
                        context.CancellationToken),
                    _logger,
                    context.CancellationToken);

                // Publish policy blocked event.
                if (_eventPublisher is not null)
                {
                    await _eventPublisher.PublishAsync(
                        new PolicyBlockedBusEvent(
                            context.Metadata.ExecutionId,
                            policy.Name,
                            reason),
                        context.CancellationToken);
                }

                var blockedResult = new AgentResult(false, null, reason, ExecutionSteps.Get(context), FailureKind: EFailureKind.PolicyBlocked);

                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnRunCompletedAsync(
                        blockedResult,
                        agentContext,
                        context.CancellationToken),
                    _logger,
                    context.CancellationToken);

                return blockedResult;
            }
        }

        return await next(context);
    }
}
