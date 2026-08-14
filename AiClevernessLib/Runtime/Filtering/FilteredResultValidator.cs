using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredResultValidator : IAgentResultValidator, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentResultValidator _inner;

    public string Name => _inner.Name;

    public FilteredResultValidator(IAgentResultValidator inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public Task<ValidationResult> ValidateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.ValidateAsync(result, context, cancellationToken);
}
