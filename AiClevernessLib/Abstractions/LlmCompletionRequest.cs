using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>Provider-neutral request for one shared LLM completion attempt.</summary>
public sealed record LlmCompletionRequest(
    string ExecutionId,
    IReadOnlyList<LlmMessage> Messages,
    LlmCompletionOptions? Options = null,
    int Turn = 0);
