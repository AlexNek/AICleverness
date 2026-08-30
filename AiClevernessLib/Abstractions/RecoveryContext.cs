using AiCleverness.Models;

namespace AiCleverness.Abstractions;

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
