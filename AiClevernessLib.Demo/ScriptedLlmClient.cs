using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Hermetic <see cref="ILlmClient"/> that replays scripted responses in FIFO order.
/// Stands in for a real provider adapter so the demo runs without any network access.
///
/// In production, replace this with an adapter for your AI provider:
///   - OpenAI (GPT-4, GPT-3.5)
///   - Anthropic (Claude)
///   - Google (Gemini)
///   - Local models (Ollama, llama.cpp)
///
/// The runtime doesn't care which provider you use — it only calls CompleteAsync().
/// </summary>
public sealed class ScriptedLlmClient : ILlmClient
{
    private readonly Queue<LlmResponse> _responses = new();

    private int _nextToolCallId = 1;

    /// <summary>Number of completion calls served so far.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"ScriptedLlmClient has no queued responses left (call #{CallCount + 1}). " +
                "Enqueue scripted responses before running the agent.");
        }

        CallCount++;
        return Task.FromResult(_responses.Dequeue());
    }

    /// <summary>
    /// Queues a plain-text response.
    /// </summary>
    public ScriptedLlmClient EnqueueText(string content)
    {
        _responses.Enqueue(new LlmResponse(content));
        return this;
    }

    /// <summary>
    /// Queues a response in which the model asks the runtime to invoke a tool.
    /// </summary>
    public ScriptedLlmClient EnqueueToolCall(string toolName, string argumentsJson)
    {
        var toolCall = new LlmToolCall($"call-{_nextToolCallId++}", toolName, argumentsJson);
        _responses.Enqueue(new LlmResponse(null, [toolCall]));
        return this;
    }

    /// <summary>
    /// Clears all queued responses and counters.
    /// </summary>
    public ScriptedLlmClient Reset()
    {
        _responses.Clear();
        CallCount = 0;
        _nextToolCallId = 1;
        return this;
    }
}
