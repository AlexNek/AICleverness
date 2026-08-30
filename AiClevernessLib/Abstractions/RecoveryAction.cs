namespace AiCleverness.Abstractions;

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
