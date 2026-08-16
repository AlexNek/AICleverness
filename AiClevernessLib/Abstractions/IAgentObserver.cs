using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Observes agent runtime lifecycle events without changing execution behavior.
/// </summary>
public interface IAgentObserver
{
    Task OnGateRejectedAsync(
        IAgentQualityGate gate,
        QualityGateResult result,
        int retryCount,
        CancellationToken cancellationToken);

    Task OnLlmCalledAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken);

    /// <summary>
    /// Called exactly once per LLM completion attempt (success, error, or timeout).
    /// Provides full context about the call including model, duration, and classification.
    /// </summary>
    Task OnLlmCallCompletedAsync(LlmCallInfo info, CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task OnLlmRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called when the runtime switches from one model to another due to failover.
    /// </summary>
    Task OnModelSwitchedAsync(
        string fromModel,
        string toModel,
        string reason,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task OnPolicyBlockedAsync(
        IAgentPolicy policy,
        PolicyResult result,
        CancellationToken cancellationToken);

    Task OnRunCompletedAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken);

    Task OnRunStartedAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken);

    Task OnToolCompletedAsync(
        ITool tool,
        ToolResult result,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task OnToolInvokedAsync(
        ITool tool,
        ToolInvocation invocation,
        CancellationToken cancellationToken);
}
