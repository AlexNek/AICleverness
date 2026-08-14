using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

/// <summary>
/// Configurable fake <see cref="AiCleverness.Abstractions.ILlmClient"/> for testing.
/// Returns scripted responses in order, or a fixed response if only one is configured.
/// Tracks all calls for assertion.
/// </summary>
public sealed class FakeChatClient : AiCleverness.Abstractions.ILlmClient
{
    private readonly List<FakeCallRecord> _calls = [];

    private readonly Queue<LlmResponse> _responses = new();

    private LlmResponse? _defaultResponse;

    /// <summary>Number of times CompleteAsync was called.</summary>
    public int CallCount => _calls.Count;

    /// <summary>All calls made to this client, in order.</summary>
    public IReadOnlyList<FakeCallRecord> Calls => _calls;

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        if (response is null)
        {
            throw new InvalidOperationException(
                $"FakeChatClient has no more queued responses and no default response set. " +
                $"Call #{CallCount + 1}. Enqueue responses with {nameof(EnqueueResponse)}().");
        }

        _calls.Add(
            new FakeCallRecord(
                messages,
                tools,
                options,
                response));

        return Task.FromResult(response);
    }

    /// <summary>
    /// Queues a response to be returned on the next call.
    /// Responses are consumed in FIFO order. If the queue is empty,
    /// the default response is used.
    /// </summary>
    public FakeChatClient EnqueueResponse(LlmResponse response)
    {
        _responses.Enqueue(response);
        return this;
    }

    /// <summary>
    /// Queues a simple text response.
    /// </summary>
    public FakeChatClient EnqueueResponse(string content)
    {
        _responses.Enqueue(new LlmResponse(content));
        return this;
    }

    /// <summary>
    /// Queues a response with tool calls.
    /// </summary>
    public FakeChatClient EnqueueToolCallResponse(params LlmToolCall[] toolCalls)
    {
        _responses.Enqueue(new LlmResponse(null, toolCalls));
        return this;
    }

    /// <summary>
    /// Clears all queued responses and call history.
    /// </summary>
    public FakeChatClient Reset()
    {
        _responses.Clear();
        _defaultResponse = null;
        _calls.Clear();
        return this;
    }

    /// <summary>
    /// Sets the default response returned when the queue is empty.
    /// </summary>
    public FakeChatClient SetDefaultResponse(LlmResponse response)
    {
        _defaultResponse = response;
        return this;
    }

    /// <summary>
    /// Sets a simple text default response.
    /// </summary>
    public FakeChatClient SetDefaultResponse(string content)
    {
        _defaultResponse = new LlmResponse(content);
        return this;
    }
}

/// <summary>
/// Record of a single call to <see cref="FakeChatClient"/>.
/// </summary>
public sealed record FakeCallRecord(
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools,
    LlmCompletionOptions? Options,
    LlmResponse Response)
{
    /// <summary>Total message count in the call.</summary>
    public int MessageCount => Messages.Count;

    /// <summary>The system message content from the last call.</summary>
    public string? SystemMessage => Messages.FirstOrDefault(m => m.Role == "system")?.Content;

    /// <summary>The user message content from the last call.</summary>
    public string? UserMessage => Messages.LastOrDefault(m => m.Role == "user")?.Content;
}
