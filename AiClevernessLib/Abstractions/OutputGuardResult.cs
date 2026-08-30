namespace AiCleverness.Abstractions;

/// <summary>
/// Result of an output guard evaluation.
/// </summary>
public sealed record OutputGuardResult(
    bool IsSafe,
    string? Reason = null,
    string? SanitizedOutput = null)
{
    /// <summary>
    /// When true and <see cref="SanitizedOutput"/> is not null,
    /// the sanitized output should replace the original.
    /// </summary>
    public bool HasSanitizedReplacement => IsSafe && SanitizedOutput is not null;
}
