using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Strategy for recovering from errors during execution.
/// Implementations decide whether to retry, skip, compensate, or abort.
/// </summary>
public interface IRecoveryStrategy
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Classifies an error and determines the recovery action.
    /// </summary>
    Task<RecoveryDecision> EvaluateAsync(
        RecoveryContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context provided to a recovery strategy for decision-making.
/// </summary>
public sealed record RecoveryContext(
    Exception Error,
    string Phase,
    int AttemptNumber,
    int MaxAttempts,
    RetryClassification Classification,
    ResourceUsage? CurrentUsage = null,
    ResourceLimits? Limits = null);

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

/// <summary>
/// Action to take when recovering from an error.
/// </summary>
public enum RecoveryAction
{
    /// <summary>Retry the failed operation.</summary>
    Retry,

    /// <summary>Skip the failed step and continue with the next.</summary>
    Skip,

    /// <summary>Abort the execution entirely.</summary>
    Abort,

    /// <summary>Compensate (reverse) completed steps and abort.</summary>
    Compensate
}
