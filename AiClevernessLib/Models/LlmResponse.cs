namespace AiCleverness.Models;

/// <summary>
/// A response returned by an LLM backend.
/// </summary>
public sealed record LlmResponse(
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? FinishReason = null,
    LlmTokenUsage? Usage = null);
