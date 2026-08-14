using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Additive interface for streaming agent execution.
/// Implementations emit <see cref="AgentEvent"/> instances as execution progresses.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends agent execution with real-time streaming of events.
/// Consumers receive events as they happen rather than waiting for the full result.
/// </para>
/// <para>
/// The final event in the stream is always <see cref="RunCompletedEvent"/>,
/// <see cref="CancellationEvent"/>, or <see cref="FailureEvent"/>.
/// </para>
/// <para>
/// Implementations that also implement <see cref="IAgentRuntime"/> should produce
/// identical results from both <c>RunAsync</c> and <c>RunStreamingAsync</c>.
/// </para>
/// </remarks>
public interface IStreamingAgentRuntime
{
    /// <summary>
    /// Executes an agent request and streams events as execution progresses.
    /// </summary>
    /// <param name="request">The agent request to execute.</param>
    /// <param name="cancellationToken">Token to cancel execution.</param>
    /// <returns>An async enumerable of agent events.</returns>
    IAsyncEnumerable<AgentEvent> RunStreamingAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}
