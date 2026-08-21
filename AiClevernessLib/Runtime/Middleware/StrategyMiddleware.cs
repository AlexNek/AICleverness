using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Transcript;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Middleware;

/// <summary>
/// Pipeline middleware that attempts registered strategies before falling through to the LLM tool loop.
/// If any strategy succeeds, the pipeline is short-circuited with the strategy result.
/// </summary>
internal sealed class StrategyMiddleware : IAgentPipelineMiddleware
{
    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentStrategy> _strategies;

    public string Name => "Strategy";

    public StrategyMiddleware(IEnumerable<IAgentStrategy> strategies, ILogger? logger = null)
    {
        _strategies = strategies;
        _logger = logger;
    }

    public async Task<AgentResult> InvokeAsync(
        IExecutionContext context,
        AgentPipelineDelegate next)
    {
        var agentContext = context.AgentContext;

        foreach (var strategy in _strategies.Where(s => s.CanExecute(agentContext)))
        {
            _logger?.LogDebug("Trying strategy {StrategyName}", strategy.Name);
            var result = await strategy.ExecuteAsync(agentContext, context.CancellationToken);

            foreach (var artifact in result.Artifacts)
            {
                ExecutionSteps.Add(context, artifact);
            }

            if (result.Success)
            {
                context.State.MarkCompleted(ExecutionStatus.Completed);

                // Emit streaming event when running under the streaming entry point.
                var emit =
                    context.Items.Get<Action<AgentEvent>>(ExecutionItemKeys.EventEmitter);
                emit?.Invoke(new ModelChunkEvent
                                 {
                                     ExecutionId = context.Metadata.ExecutionId,
                                     Content = result.Output ?? string.Empty,
                                     Turn = 0,
                                     IsFinal = true
                                 });

                var steps = ExecutionSteps.Get(context);
                context.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript)
                    ?.AppendModelContent(result.Output);
                return new AgentResult(true, result.Output, null, steps, FailureKind: EFailureKind.NoFailure);
            }
        }

        return await next(context);
    }
}
