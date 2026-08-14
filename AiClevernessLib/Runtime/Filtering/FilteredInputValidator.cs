using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Filtering;

internal sealed class FilteredInputValidator : IAgentInputValidator, IAppliesToAgent
{
    private readonly Func<IAgentContext, bool> _filter;

    private readonly IAgentInputValidator _inner;

    public string Name => _inner.Name;

    public FilteredInputValidator(IAgentInputValidator inner, Func<IAgentContext, bool> filter)
    {
        _inner = inner;
        _filter = filter;
    }

    public bool AppliesTo(IAgentContext context) => _filter(context);

    public Task<InputValidationResult> ValidateAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken) =>
        _inner.ValidateAsync(request, context, cancellationToken);
}
