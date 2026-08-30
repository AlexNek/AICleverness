using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Request for human-in-the-loop approval.
/// </summary>
public sealed record ApprovalRequest(
    string ToolName,
    ToolInvocation Invocation,
    DangerLevel DangerLevel,
    string? Reason = null,
    string? ExecutionId = null);
