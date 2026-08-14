using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Middleware;

/// <summary>
/// Pipeline middleware that invokes the planner before execution.
/// Planning failures are non-fatal; the pipeline continues without a plan.
/// </summary>
internal sealed class PlanningMiddleware : IAgentPipelineMiddleware
{
    private readonly ILogger? _logger;

    private readonly IAgentPlanner? _planner;

    public string Name => "Planning";

    public PlanningMiddleware(IAgentPlanner? planner, ILogger? logger = null)
    {
        _planner = planner;
        _logger = logger;
    }

    public async Task<AgentResult> InvokeAsync(
        IExecutionContext context,
        AgentPipelineDelegate next)
    {
        if (_planner is null)
            return await next(context);

        try
        {
            var plan = await _planner.PlanAsync(
                           context.Metadata.Request,
                           context.AgentContext,
                           context.CancellationToken);
            var planMsg = $"Planner produced {plan.Count} step(s).";
            ExecutionSteps.Add(context, planMsg);
            context.AgentContext.State.Set(ExecutionItemKeys.Plan, plan);
            context.Items.Set(ExecutionItemKeys.Plan, plan);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Planner failed; continuing without plan.");
            ExecutionSteps.Add(context, $"Planner failed: {ex.Message}");
        }

        return await next(context);
    }
}
