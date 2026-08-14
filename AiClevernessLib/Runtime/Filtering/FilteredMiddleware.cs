using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredMiddleware : IAgentPipelineMiddleware, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentPipelineMiddleware _inner;

    public string Name => _inner.Name;

    public FilteredMiddleware(IAgentPipelineMiddleware inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public Task<AgentResult> InvokeAsync(IExecutionContext context, AgentPipelineDelegate next) =>
        _filter(context.AgentContext) ? _inner.InvokeAsync(context, next) : next(context);
}
