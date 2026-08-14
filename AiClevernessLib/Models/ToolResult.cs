namespace AiCleverness.Models;

/// <summary>
/// Result of a tool invocation.
/// </summary>
public sealed record ToolResult(
    bool Success,
    string? Output = null,
    string? Error = null);
