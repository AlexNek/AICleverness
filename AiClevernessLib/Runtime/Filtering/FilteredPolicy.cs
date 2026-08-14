using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredPolicy : IAgentPolicy, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentPolicy _inner;

    public string Name => _inner.Name;

    public int Priority => _inner.Priority;

    public FilteredPolicy(IAgentPolicy inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context) && _inner.AppliesTo(context);

    public Task<PolicyResult> EvaluateAsync(
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.EvaluateAsync(context, cancellationToken);
}
