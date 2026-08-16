namespace AiCleverness.Models;

/// <summary>
/// An incremental tool-call fragment received during LLM streaming.
/// Fragments are indexed by position and accumulated into complete
/// <see cref="LlmToolCall"/> instances by <see cref="AiCleverness.Runtime.StreamingToolCallAccumulator"/>.
/// </summary>
/// <param name="Index">Zero-based position identifying which tool call this fragment belongs to.</param>
/// <param name="Id">The tool call identifier (typically only present in the first fragment for an index).</param>
/// <param name="Name">The function/tool name (typically only present in the first fragment for an index).</param>
/// <param name="ArgumentsFragment">A JSON fragment of the arguments string to append.</param>
public sealed record LlmToolCallDelta(
    int Index,
    string? Id = null,
    string? Name = null,
    string? ArgumentsFragment = null);
