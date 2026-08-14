using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredTransformer : IAgentResultTransformer, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentResultTransformer _inner;

    public string Name => _inner.Name;

    public int Priority => _inner.Priority;

    public FilteredTransformer(IAgentResultTransformer inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public Task<AgentResult> TransformAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.TransformAsync(result, context, cancellationToken);
}
