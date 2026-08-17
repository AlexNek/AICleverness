using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Workflows;

/// <summary>
/// Executes multiple agent requests sequentially, passing the output of each
/// as context to the next. Implements a simple chained-agent pattern.
/// </summary>
public sealed class SequentialAgentPipeline
{
    private readonly IAgentRuntime _runtime;

    public SequentialAgentPipeline(IAgentRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>
    /// Runs agent requests in sequence. Each request's goal can reference
    /// the previous output via the {{previous_output}} placeholder in goals.
    /// </summary>
    public async Task<AgentResult> RunAsync(
        IReadOnlyList<AgentRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return new AgentResult(true, null, "Empty pipeline.", FailureKind: EFailureKind.NoFailure);

        AgentResult? lastResult = null;

        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var goal = lastResult is not null
                           ? request.Goal.Replace(
                               "{{previous_output}}",
                               lastResult.Output ?? string.Empty,
                               StringComparison.Ordinal)
                           : request.Goal;

            var adjustedRequest = request with { Goal = goal };
            lastResult = await _runtime.RunAsync(adjustedRequest, null, cancellationToken);

            if (!lastResult.Success)
                return lastResult;
        }

        return lastResult!;
    }
}
