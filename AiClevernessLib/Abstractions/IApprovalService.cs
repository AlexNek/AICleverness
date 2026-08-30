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
