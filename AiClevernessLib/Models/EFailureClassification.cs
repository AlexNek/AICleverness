namespace AiCleverness.Models;

/// <summary>
/// Describes how the runtime should treat a completion failure.
/// </summary>
public enum EFailureClassification
{
    /// <summary>Permanent failure — abort immediately, do not failover.</summary>
    Permanent,

    /// <summary>
    /// Transient failure — advance to the next candidate model.
    /// The current model is NOT retried; the chain always moves forward.
    /// </summary>
    TransientAdvance
}
