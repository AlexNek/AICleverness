using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Service for requesting human-in-the-loop approval before executing sensitive operations.
/// Implementations may use interactive prompts, queues, webhooks, or auto-approve policies.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Requests approval for a tool invocation.
    /// </summary>
    /// <param name="request">Details about what needs approval.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approval decision.</returns>
    Task<ApprovalDecision> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for human-in-the-loop approval.
/// </summary>
public sealed record ApprovalRequest(
    string ToolName,
    ToolInvocation Invocation,
    DangerLevel DangerLevel,
    string? Reason = null,
    string? ExecutionId = null);

/// <summary>
/// Decision from the approval service.
/// </summary>
public sealed record ApprovalDecision(
    bool Approved,
    string? Reason = null,
    string? ApprovedBy = null,
    DateTimeOffset? DecidedAt = null)
{
    /// <summary>Creates an auto-approved decision.</summary>
    public static ApprovalDecision AutoApproved(string? reason = null) =>
        new(true, reason ?? "Auto-approved", "system", DateTimeOffset.UtcNow);

    /// <summary>Creates a denied decision.</summary>
    public static ApprovalDecision Denied(string? reason = null) =>
        new(false, reason ?? "Approval denied", null, DateTimeOffset.UtcNow);
}
