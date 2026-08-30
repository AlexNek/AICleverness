namespace AiCleverness.Abstractions;

/// <summary>
/// Decision from a recovery strategy.
/// </summary>
public sealed record RecoveryDecision(
    RecoveryAction Action,
    TimeSpan? DelayBeforeRetry = null,
    string? Reason = null)
{
    /// <summary>Abort the execution.</summary>
    public static RecoveryDecision Abort(string? reason = null) =>
        new(RecoveryAction.Abort, null, reason);

    /// <summary>Compensate (roll back) and abort.</summary>
    public static RecoveryDecision Compensate(string? reason = null) =>
        new(RecoveryAction.Compensate, null, reason);

    /// <summary>Retry the operation.</summary>
    public static RecoveryDecision Retry(TimeSpan? delay = null, string? reason = null) =>
        new(RecoveryAction.Retry, delay, reason);

    /// <summary>Skip the current step and continue.</summary>
    public static RecoveryDecision Skip(string? reason = null) =>
        new(RecoveryAction.Skip, null, reason);
}
