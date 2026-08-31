using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Result of a tool-call validation.
/// </summary>
public sealed record ToolCallValidationResult(
    bool IsAllowed,
    string? Reason = null,
    DangerLevel DangerLevel = DangerLevel.Safe);
