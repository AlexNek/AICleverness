using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

internal static class LlmMessageSizeEstimator
{
    private const int MessageOverheadCharacters = 4;
    private const int ToolCallOverheadCharacters = 10;

    public static int EstimateTokens(IReadOnlyList<LlmMessage> messages, int charsPerToken)
    {
        var totalChars = 0;
        foreach (var message in messages)
            totalChars += EstimateChars(message);

        return totalChars / charsPerToken;
    }

    public static int EstimateChars(LlmMessage message)
    {
        var chars = (message.Content?.Length ?? 0) + message.Role.Length + MessageOverheadCharacters;
        if (message.ToolCalls is { Count: > 0 })
        {
            foreach (var toolCall in message.ToolCalls)
                chars += toolCall.Name.Length + (toolCall.Arguments?.Length ?? 0) + ToolCallOverheadCharacters;
        }

        return chars;
    }
}
