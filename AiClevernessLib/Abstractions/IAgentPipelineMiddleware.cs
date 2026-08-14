using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Delegate representing the next step in the agent execution pipeline.
/// </summary>
public delegate Task<AgentResult> AgentPipelineDelegate(IExecutionContext context);

/// <summary>
/// A middleware component in the agent execution pipeline.
/// Middleware can inspect/modify the context, short-circuit execution, or delegate to the next component.
/// </summary>
/// <remarks>
/// <para>
/// Middleware is composed in registration order. Each middleware receives the execution context and a
/// delegate to the next middleware in the chain. The terminal delegate runs the LLM tool loop.
/// </para>
/// <para>
/// A middleware may:
/// <list type="bullet">
/// <item>Short-circuit by returning an <see cref="AgentResult"/> without calling the <see cref="AgentPipelineDelegate"/>.</item>
/// <item>Modify the context before/after calling the <see cref="AgentPipelineDelegate"/>.</item>
/// <item>Transform the result returned by the <see cref="AgentPipelineDelegate"/>.</item>
/// </list>
/// </para>
/// </remarks>
public interface IAgentPipelineMiddleware
{
    /// <summary>Display name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Invokes this middleware step.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="next">Delegate to the next middleware or terminal handler.</param>
    /// <returns>The agent result, potentially modified or short-circuited.</returns>
    Task<AgentResult> InvokeAsync(IExecutionContext context, AgentPipelineDelegate next);
}
