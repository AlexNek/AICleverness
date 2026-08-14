namespace AiCleverness.Models;

/// <summary>
/// A message exchanged with an LLM backend.
/// </summary>
public sealed record LlmMessage(
    string Role,
    string? Content = null)
{
    /// <summary>
    /// For tool response messages, the ID of the tool call being answered.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// For assistant messages, the tool calls requested by the model.
    /// </summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }
}
