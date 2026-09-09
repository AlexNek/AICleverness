namespace AiCleverness.Models;

/// <summary>
/// A response returned by an LLM backend.
/// </summary>
/// <param name="Content">The primary text content of the response, if any.</param>
/// <param name="ToolCalls">Tool calls requested by the model, if any.</param>
/// <param name="FinishReason">The provider-supplied reason the completion ended, if any.</param>
/// <param name="Usage">Token usage reported by the provider, if any.</param>
/// <param name="ReasoningContent">
/// Optional chain-of-thought reasoning supplied by providers that return a separate
/// <c>reasoning_content</c> field (e.g. DeepSeek). Purely informational: it is carried
/// through the runtime for observers, transcripts, and debug output and does not affect
/// executor, planner, or tool-loop behavior. Defaults to <see langword="null"/> so existing
/// callers remain source- and binary-compatible.
/// </param>
public sealed record LlmResponse(
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? FinishReason = null,
    LlmTokenUsage? Usage = null,
    string? ReasoningContent = null);
