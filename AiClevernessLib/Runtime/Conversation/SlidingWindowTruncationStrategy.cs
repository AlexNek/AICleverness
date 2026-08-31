using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

/// <summary>
/// Truncation strategy that keeps the system message and the most recent messages
/// within the token budget (sliding window from the end).
/// </summary>
public sealed class SlidingWindowTruncationStrategy : ITruncationStrategy
{
    private readonly int _charsPerToken;

    public string Name => "SlidingWindow";

    /// <param name="charsPerToken">Approximate characters per token for estimation (default: 4).</param>
    public SlidingWindowTruncationStrategy(int charsPerToken = 4)
    {
        _charsPerToken = charsPerToken > 0 ? charsPerToken : 4;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LlmMessage> Truncate(IReadOnlyList<LlmMessage> messages, int maxTokens)
    {
        if (messages.Count == 0) return messages;

        var maxChars = maxTokens * _charsPerToken;
        var result = new List<LlmMessage>();
        var usedChars = 0;

        // Always keep the system message (first message if role == "system").
        var startIndex = 0;
        if (messages[0].Role == LlmMessageRoles.System)
        {
            result.Add(messages[0]);
            usedChars += EstimateChars(messages[0]);
            startIndex = 1;
        }

        // Work backwards from the end, adding messages until budget is exhausted.
        var tail = new List<LlmMessage>();
        for (var i = messages.Count - 1; i >= startIndex; i--)
        {
            var msgChars = EstimateChars(messages[i]);
            if (usedChars + msgChars > maxChars)
                break;
            tail.Add(messages[i]);
            usedChars += msgChars;
        }

        tail.Reverse();
        result.AddRange(tail);
        return result;
    }

    private static int EstimateChars(LlmMessage message)
        => LlmMessageSizeEstimator.EstimateChars(message);
}
