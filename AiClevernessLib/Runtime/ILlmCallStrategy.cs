using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Strategy for performing an LLM call within the tool loop.
/// Encapsulates timeout semantics and response aggregation.
/// </summary>
/// <remarks>
/// Two implementations exist:
/// <list type="bullet">
/// <item><see cref="BufferedLlmCallStrategy"/> — wall-clock timeout, no streaming.</item>
/// <item><see cref="StreamingLlmCallStrategy"/> — idle timeout with chunk aggregation.</item>
/// </list>
/// </remarks>
internal interface ILlmCallStrategy
{
    /// <summary>
    /// Executes an LLM call and returns the aggregated response.
    /// </summary>
    /// <param name="messages">Conversation messages.</param>
    /// <param name="tools">Available tool definitions.</param>
    /// <param name="options">LLM completion options.</param>
    /// <param name="strategyOptions">Timeout and callback configuration.</param>
    /// <param name="cancellationToken">Caller cancellation token.</param>
    /// <returns>The LLM response.</returns>
    Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        LlmCompletionOptions? options,
        LlmCallStrategyOptions strategyOptions,
        CancellationToken cancellationToken);
}
