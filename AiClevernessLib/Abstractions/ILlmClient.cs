using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Provider-neutral client for LLM completions. Implementations adapt concrete
/// AI providers (OpenAI, Anthropic, local models, etc.) to the AiCleverness runtime.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a chat completion request and returns the model's response.
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
