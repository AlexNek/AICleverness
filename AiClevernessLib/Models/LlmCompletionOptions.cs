namespace AiCleverness.Models;

/// <summary>
/// Provider-neutral options for an LLM completion request.
/// </summary>
public sealed record LlmCompletionOptions(
    float Temperature = 0.7f,
    int? MaxTokens = null,
    string? Model = null);
