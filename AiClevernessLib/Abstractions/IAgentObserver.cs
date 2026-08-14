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

    Task OnLlmRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken);

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
