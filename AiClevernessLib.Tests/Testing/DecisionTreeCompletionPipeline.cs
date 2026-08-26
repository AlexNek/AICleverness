using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeCompletionPipeline : ILlmCompletionPipeline
{
    private readonly Queue<LlmResponse> _responses = new();

    public int CallCount { get; private set; }

    public DecisionTreeCompletionPipeline Enqueue(string content)
    {
        _responses.Enqueue(new LlmResponse(content));
        return this;
    }

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_responses.Dequeue());
    }
}
