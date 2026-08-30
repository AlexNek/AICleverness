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
