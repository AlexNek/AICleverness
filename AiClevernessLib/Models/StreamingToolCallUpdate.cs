namespace AiCleverness.Models;

/// <summary>
/// A partial update for a streaming tool call.
/// Accumulates into a <see cref="CompletedToolCall"/> when all chunks are received.
/// </summary>
public sealed record StreamingToolCallUpdate(
    string ToolCallId,
    string? FunctionName = null,
    string? ArgumentsChunk = null);
