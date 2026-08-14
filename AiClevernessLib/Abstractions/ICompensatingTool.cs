using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Extension of <see cref="ITool"/> for tools that support compensation (rollback).
/// When an execution fails after a tool has completed, the compensation action
/// can reverse the tool's side effects.
/// </summary>
public interface ICompensatingTool : ITool
{
    /// <summary>
    /// Whether this tool supports compensation for the given invocation.
    /// </summary>
    bool CanCompensate(ToolInvocation originalInvocation, ToolResult originalResult);

    /// <summary>
    /// Compensates (reverses) a previously successful tool invocation.
    /// </summary>
    Task<ToolResult> CompensateAsync(
        ToolInvocation originalInvocation,
        ToolResult originalResult,
        CancellationToken cancellationToken = default);
}
