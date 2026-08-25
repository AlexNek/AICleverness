using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>Shared provider-neutral LLM completion boundary.</summary>
public interface ILlmCompletionPipeline
{
    Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);
}
