namespace AiCleverness.Models;

/// <summary>
/// A single chunk received during LLM streaming.
/// Carries a content delta, optional tool-call fragments, a completion flag,
/// and optional token usage (typically only present on the final chunk).
/// </summary>
public sealed record LlmChunk(
    string? Content,
    IReadOnlyList<LlmToolCallDelta>? ToolCalls = null,
    bool IsCompleted = false,
    LlmTokenUsage? Usage = null);
