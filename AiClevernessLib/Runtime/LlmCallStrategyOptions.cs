using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Configuration passed to an <see cref="ILlmCallStrategy"/> for a single LLM call.
/// </summary>
/// <param name="CompletionTimeoutSeconds">
/// Absolute wall-clock timeout. For buffered calls this is the total allowed duration.
/// For streaming calls this is the safety cap — the stream is killed if it exceeds this
/// regardless of chunk activity.
/// </param>
/// <param name="IdleTimeoutSeconds">
/// Silence threshold between meaningful chunks during streaming.
/// Ignored by the buffered strategy.
/// </param>
/// <param name="OnChunk">
/// Optional callback invoked for each meaningful content chunk during streaming.
/// Allows the caller to emit real-time progress events.
/// </param>
internal sealed record LlmCallStrategyOptions(
    int CompletionTimeoutSeconds,
    int IdleTimeoutSeconds,
    Action<string>? OnChunk = null);
