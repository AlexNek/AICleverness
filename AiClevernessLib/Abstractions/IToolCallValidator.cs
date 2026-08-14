using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Validates a tool invocation before it is executed.
/// Can block dangerous or unauthorized tool calls.
/// </summary>
public interface IToolCallValidator
{
    /// <summary>Display name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Validates whether a tool invocation should proceed.
    /// </summary>
    Task<ToolCallValidationResult> ValidateAsync(
        ITool tool,
        ToolInvocation invocation,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a tool-call validation.
/// </summary>
public sealed record ToolCallValidationResult(
    bool IsAllowed,
    string? Reason = null,
    DangerLevel DangerLevel = DangerLevel.Safe);
