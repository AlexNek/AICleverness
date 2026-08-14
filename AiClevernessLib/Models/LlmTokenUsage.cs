namespace AiCleverness.Models;

/// <summary>
/// Token usage reported by an LLM completion.
/// </summary>
public sealed record LlmTokenUsage(
    int PromptTokens,
    int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}
