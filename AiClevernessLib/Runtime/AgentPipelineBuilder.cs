using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Builds a composed agent execution pipeline from an ordered list of middleware and a terminal handler.
/// </summary>
/// <remarks>
/// Middleware is invoked in registration order (first registered = outermost wrapper).
/// The terminal handler is always the innermost step (typically the LLM tool loop).
/// </remarks>
internal sealed class AgentPipelineBuilder
{
    private readonly List<IAgentPipelineMiddleware> _middleware = new();

    private AgentPipelineDelegate? _terminal;

    /// <summary>
    /// Builds the composed pipeline delegate.
    /// </summary>
    /// <returns>A delegate that runs the full pipeline from outermost middleware to terminal.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no terminal handler has been set.</exception>
    public AgentPipelineDelegate Build()
    {
        if (_terminal is null)
            throw new InvalidOperationException(
                "Pipeline requires a terminal handler. Call UseTerminal() before Build().");

        // Build from inside out: terminal first, then wrap with middleware in reverse order.
        var pipeline = _terminal;

        for (var i = _middleware.Count - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = pipeline;
            pipeline = context => middleware.InvokeAsync(context, next);
        }

        return pipeline;
    }

    /// <summary>Appends a middleware to the pipeline (executed in order).</summary>
    public AgentPipelineBuilder Use(IAgentPipelineMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middleware.Add(middleware);
        return this;
    }

    /// <summary>Appends multiple middleware in order.</summary>
    public AgentPipelineBuilder Use(IEnumerable<IAgentPipelineMiddleware> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        foreach (var m in middleware)
        {
            Use(m);
        }

        return this;
    }

    /// <summary>Sets the terminal handler (the innermost step, usually the LLM tool loop).</summary>
    public AgentPipelineBuilder UseTerminal(AgentPipelineDelegate terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        _terminal = terminal;
        return this;
    }
}
