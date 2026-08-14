namespace AiCleverness.Abstractions;

/// <summary>
/// Guards against unsafe, toxic, or policy-violating content in the LLM output
/// before it is returned to the caller.
/// </summary>
public interface IOutputGuard
{
    /// <summary>Display name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the LLM output for safety and policy compliance.
    /// </summary>
    Task<OutputGuardResult> EvaluateAsync(
        string output,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}

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
