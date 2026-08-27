using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

internal static class LlmMessageSizeEstimator
{
    public static int EstimateTokens(IReadOnlyList<LlmMessage> messages, int charsPerToken)
    {
        var totalChars = 0;
        foreach (var message in messages)
            totalChars += EstimateChars(message);

        return totalChars / charsPerToken;
    }

    public static int EstimateChars(LlmMessage message)
    {
        var chars = (message.Content?.Length ?? 0) + message.Role.Length + 4;
        if (message.ToolCalls is { Count: > 0 })
        {
            foreach (var toolCall in message.ToolCalls)
                chars += toolCall.Name.Length + (toolCall.Arguments?.Length ?? 0) + 10;
        }

        return chars;
    }
}
