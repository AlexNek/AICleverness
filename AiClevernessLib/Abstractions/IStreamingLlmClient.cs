using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Optional streaming extension of <see cref="ILlmClient"/>.
/// Implementations that support token-level streaming implement this interface
/// in addition to <see cref="ILlmClient"/>. The runtime uses streaming when available
/// and falls back to buffered <see cref="ILlmClient.CompleteAsync"/> otherwise.
/// </summary>
/// <remarks>
/// Because this interface inherits <see cref="ILlmClient"/>, every streaming
/// implementation is guaranteed to also provide the non-streaming path.
/// Implementations should apply <c>[EnumeratorCancellation]</c> to the
/// <c>cancellationToken</c> parameter on their async-iterator method.
/// </remarks>
public interface IStreamingLlmClient : ILlmClient
{
    /// <summary>
    /// Sends a chat completion request and returns the model's response as a stream of chunks.
    /// Each chunk carries a content delta, optional tool-call fragments, and a completion flag.
    /// </summary>
    /// <param name="messages">The conversation messages.</param>
    /// <param name="tools">Available tool definitions, or null if no tools.</param>
    /// <param name="options">Completion options (temperature, model, etc.).</param>
    /// <param name="cancellationToken">Token to cancel the stream mid-generation.</param>
    /// <returns>An async stream of <see cref="LlmChunk"/> instances.</returns>
    IAsyncEnumerable<LlmChunk> StreamAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
