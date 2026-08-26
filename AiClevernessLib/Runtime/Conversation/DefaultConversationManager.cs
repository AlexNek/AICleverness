using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

/// <summary>
/// Default conversation manager that tracks messages and applies truncation/summarization
/// when preparing messages for LLM calls.
/// </summary>
public sealed class DefaultConversationManager : IConversationManager
{
    private readonly int _charsPerToken;

    private readonly List<LlmMessage> _messages = new();

    private readonly ISummarizationStrategy? _summarizationStrategy;

    private readonly ITruncationStrategy _truncationStrategy;

    /// <inheritdoc/>
    public int EstimatedTokenCount
    {
        get
        {
            var totalChars = 0;
            foreach (var msg in _messages)
            {
                totalChars += (msg.Content?.Length ?? 0) + msg.Role.Length + 4;
                if (msg.ToolCalls is { Count: > 0 })
                {
                    foreach (var tc in msg.ToolCalls)
                        totalChars += tc.Name.Length + (tc.Arguments?.Length ?? 0) + 10;
                }
            }

            return totalChars / _charsPerToken;
        }
    }

    /// <inheritdoc/>
    public int MessageCount => _messages.Count;

    public DefaultConversationManager(
        ITruncationStrategy? truncationStrategy = null,
        ISummarizationStrategy? summarizationStrategy = null,
        int charsPerToken = 4)
    {
        _truncationStrategy =
            truncationStrategy ?? new SlidingWindowTruncationStrategy(charsPerToken);
        _summarizationStrategy = summarizationStrategy;
        _charsPerToken = charsPerToken > 0 ? charsPerToken : 4;
    }

    /// <summary>Creates an isolated manager that preserves this manager's configuration.</summary>
    public DefaultConversationManager CreateForExecution()
        => new(_truncationStrategy, _summarizationStrategy, _charsPerToken);

    /// <inheritdoc/>
    public void AddMessage(LlmMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
    }

    /// <inheritdoc/>
    public void AddMessages(IEnumerable<LlmMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _messages.AddRange(messages);
    }

    /// <inheritdoc/>
    public void Clear() => _messages.Clear();

    /// <inheritdoc/>
    public IReadOnlyList<LlmMessage> GetMessages() => _messages.AsReadOnly();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LlmMessage>> GetMessagesForCompletionAsync(
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        if (EstimatedTokenCount <= maxTokens)
            return _messages.AsReadOnly();

        // Try summarization first if available.
        if (_summarizationStrategy is not null)
        {
            return await _summarizationStrategy.SummarizeAsync(
                       _messages,
                       maxTokens,
                       cancellationToken);
        }

        // Fall back to truncation.
        return _truncationStrategy.Truncate(_messages, maxTokens);
    }
}
