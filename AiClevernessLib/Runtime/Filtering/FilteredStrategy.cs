using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredStrategy : IAgentStrategy, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentStrategy _inner;

    public string Name => _inner.Name;

    public FilteredStrategy(IAgentStrategy inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public bool CanExecute(IAgentContext context) => _filter(context) && _inner.CanExecute(context);

    public Task<StrategyResult> ExecuteAsync(
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.ExecuteAsync(context, cancellationToken);
}
