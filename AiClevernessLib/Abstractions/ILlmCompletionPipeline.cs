using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>Shared provider-neutral LLM completion boundary.</summary>
public interface ILlmCompletionPipeline
{
    Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a request using the shared runtime policies when execution services are supplied.
    /// Existing custom pipelines remain compatible through the default implementation.
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        LlmCompletionExecutionContext executionContext,
        CancellationToken cancellationToken = default)
        => CompleteAsync(request, cancellationToken);
}
