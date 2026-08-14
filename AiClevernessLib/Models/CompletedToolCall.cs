namespace AiCleverness.Models;

/// <summary>
/// Represents a fully accumulated tool call ready for execution.
/// Produced by the <see cref="AiCleverness.Runtime.ToolCallBuffer"/> when streaming
/// tool call arguments are syntactically complete.
/// </summary>
public sealed record CompletedToolCall(
    string Id,
    string Name,
    string Arguments);
