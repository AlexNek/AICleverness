namespace AiCleverness.Models;

/// <summary>
/// A tool call issued by an LLM.
/// </summary>
public sealed record LlmToolCall(
    string Id,
    string Name,
    string Arguments);
