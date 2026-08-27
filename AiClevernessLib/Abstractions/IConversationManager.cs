using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Manages conversation history for an execution, including truncation and summarization
/// strategies to stay within context window limits.
/// </summary>
public interface IConversationManager
{
    /// <summary>Gets the estimated token count for the current conversation.</summary>
    int EstimatedTokenCount { get; }

    /// <summary>Gets the total number of messages.</summary>
    int MessageCount { get; }

    /// <summary>Adds a message to the conversation history.</summary>
    void AddMessage(LlmMessage message);

    /// <summary>Adds multiple messages.</summary>
    void AddMessages(IEnumerable<LlmMessage> messages);

    /// <summary>Clears the conversation history.</summary>
    void Clear();

    /// <summary>Gets the current conversation messages.</summary>
    IReadOnlyList<LlmMessage> GetMessages();

    /// <summary>
    /// Gets messages prepared for an LLM call, applying truncation or summarization
    /// as needed to fit within the given token budget. Decision classification builders
    /// depend on object identity for required user messages; custom managers must return
    /// the same message instances when those messages are retained.
    /// </summary>
    Task<IReadOnlyList<LlmMessage>> GetMessagesForCompletionAsync(
        int maxTokens,
        CancellationToken cancellationToken = default);
}
