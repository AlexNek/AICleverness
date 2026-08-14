using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Security;

/// <summary>
/// Default approval service that automatically approves all requests.
/// Replace with a human-in-the-loop implementation for production use.
/// </summary>
public sealed class AutoApprovalService : IApprovalService
{
    /// <inheritdoc/>
    public Task<ApprovalDecision> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApprovalDecision.AutoApproved("Default auto-approval policy."));
    }
}
