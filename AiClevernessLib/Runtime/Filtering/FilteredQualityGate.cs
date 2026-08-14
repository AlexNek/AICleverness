using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredQualityGate : IAgentQualityGate, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentQualityGate _inner;

    public string Name => _inner.Name;

    public int Priority => _inner.Priority;

    public FilteredQualityGate(IAgentQualityGate inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context) && _inner.AppliesTo(context);

    public Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.EvaluateAsync(result, context, cancellationToken);
}
