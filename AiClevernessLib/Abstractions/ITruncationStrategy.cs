using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Strategy for truncating conversation history to fit within a token budget.
/// </summary>
public interface ITruncationStrategy
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Truncates messages to fit within the given token budget.
    /// The system message (first message) is typically preserved.
    /// </summary>
    IReadOnlyList<LlmMessage> Truncate(
        IReadOnlyList<LlmMessage> messages,
        int maxTokens);
}
