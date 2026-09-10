using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

/// <summary>
/// Completion pipeline test double whose completion calls always throw, used to
/// simulate an LLM timeout or connection failure during decision-tree classification.
/// </summary>
internal sealed class ThrowingCompletionPipeline : ILlmCompletionPipeline
{
    private readonly Func<Exception> _exceptionFactory;

    public ThrowingCompletionPipeline(Func<Exception> exceptionFactory)
    {
        _exceptionFactory = exceptionFactory;
    }

    public int CallCount { get; private set; }

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        throw _exceptionFactory();
    }

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        LlmCompletionExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        throw _exceptionFactory();
    }
}
