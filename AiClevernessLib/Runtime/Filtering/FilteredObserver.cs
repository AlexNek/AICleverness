using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredObserver : IAgentObserver, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentObserver _inner;

    private IAgentContext? _currentContext;

    public FilteredObserver(IAgentObserver inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public Task OnGateRejectedAsync(
        IAgentQualityGate gate,
        QualityGateResult result,
        int retryCount,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnGateRejectedAsync(gate, result, retryCount, cancellationToken)
            : Task.CompletedTask;

    public Task OnLlmCalledAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnLlmCalledAsync(messages, cancellationToken)
            : Task.CompletedTask;

    public Task OnLlmCallCompletedAsync(
        LlmCallInfo info,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnLlmCallCompletedAsync(info, cancellationToken)
            : Task.CompletedTask;

    public Task OnLlmRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnLlmRespondedAsync(response, duration, cancellationToken)
            : Task.CompletedTask;

    public Task OnModelSwitchedAsync(
        string fromModel,
        string toModel,
        string reason,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnModelSwitchedAsync(fromModel, toModel, reason, cancellationToken)
            : Task.CompletedTask;

    public Task OnPolicyBlockedAsync(
        IAgentPolicy policy,
        PolicyResult result,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnPolicyBlockedAsync(policy, result, cancellationToken)
            : Task.CompletedTask;

    public Task OnRunCompletedAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _filter(context)
            ? _inner.OnRunCompletedAsync(result, context, cancellationToken)
            : Task.CompletedTask;

    public Task OnRunStartedAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        _currentContext = context;
        return _filter(context)
                   ? _inner.OnRunStartedAsync(request, context, cancellationToken)
                   : Task.CompletedTask;
    }

    public Task OnToolCompletedAsync(
        ITool tool,
        ToolResult result,
        TimeSpan duration,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnToolCompletedAsync(tool, result, duration, cancellationToken)
            : Task.CompletedTask;

    public Task OnToolInvokedAsync(
        ITool tool,
        ToolInvocation invocation,
        CancellationToken cancellationToken) =>
        _currentContext is not null && _filter(_currentContext)
            ? _inner.OnToolInvokedAsync(tool, invocation, cancellationToken)
            : Task.CompletedTask;
}
