namespace AiCleverness.Abstractions;

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
