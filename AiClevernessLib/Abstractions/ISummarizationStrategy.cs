using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Strategy for summarizing older conversation history to reduce token usage
/// while preserving important context.
/// </summary>
public interface ISummarizationStrategy
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Summarizes messages to reduce token count while preserving key information.
    /// Returns a condensed set of messages.
    /// </summary>
    Task<IReadOnlyList<LlmMessage>> SummarizeAsync(
        IReadOnlyList<LlmMessage> messages,
        int targetTokens,
        CancellationToken cancellationToken = default);
}
