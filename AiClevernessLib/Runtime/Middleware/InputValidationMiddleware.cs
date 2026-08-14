using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Filtering;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Middleware;

/// <summary>
/// Pipeline middleware that runs input validators before execution.
/// Global validators run on all agents; filtered validators only on matching agents.
/// </summary>
internal sealed class InputValidationMiddleware : IAgentPipelineMiddleware
{
    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentInputValidator> _validators;

    public string Name => "InputValidation";

    public InputValidationMiddleware(
        IEnumerable<IAgentInputValidator> validators,
        ILogger? logger = null)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<AgentResult> InvokeAsync(
        IExecutionContext context,
        AgentPipelineDelegate next)
    {
        var agentContext = context.AgentContext;
        var request = context.Metadata.Request;

        foreach (var validator in _validators)
        {
            // Check scoping filter.
            if (validator is IAppliesToAgent scoped && !scoped.AppliesTo(agentContext))
                continue;

            var result = await validator.ValidateAsync(
                             request,
                             agentContext,
                             context.CancellationToken);
            if (!result.IsValid)
            {
                _logger?.LogDebug(
                    "Input validator {ValidatorName} rejected request: {Error}",
                    validator.Name,
                    result.Error);
                context.State.MarkCompleted(ExecutionStatus.Blocked);
                context.State.StatusDetail = $"Input validation failed: {result.Error}";

                var steps = ExecutionSteps.Get(context);
                steps.Add($"Input validation failed ({validator.Name}): {result.Error}");

                return new AgentResult(false, null, result.Error, steps);
            }
        }

        return await next(context);
    }
}
