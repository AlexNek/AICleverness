using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Runtime;

public sealed class TranscriptTestLlmClient : ILlmClient
{
    private readonly Exception? _exception;

    private readonly ConcurrentQueue<LlmResponse> _responses;

    public TranscriptTestLlmClient(params LlmResponse[] responses)
    {
        _responses = new ConcurrentQueue<LlmResponse>(responses);
    }

    public TranscriptTestLlmClient(Exception exception)
    {
        _exception = exception;
        _responses = new ConcurrentQueue<LlmResponse>();
    }

    public Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_exception is not null)
            throw _exception;

        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        throw new InvalidOperationException("Transcript test client has no scripted response.");
    }
}
