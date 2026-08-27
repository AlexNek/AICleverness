using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeCompletionPipeline : ILlmCompletionPipeline
{
    private readonly Queue<LlmResponse> _responses = new();

    public int CallCount { get; private set; }

    public int NoContextCallCount { get; private set; }

    public int ContextCallCount { get; private set; }

    public List<LlmCompletionRequest> Requests { get; } = [];

    public List<LlmCompletionExecutionContext> Contexts { get; } = [];

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
        NoContextCallCount++;
        Requests.Add(request);
        return Task.FromResult(_responses.Dequeue());
    }

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        LlmCompletionExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        ContextCallCount++;
        Requests.Add(request);
        Contexts.Add(executionContext);
        return Task.FromResult(_responses.Dequeue());
    }
}
