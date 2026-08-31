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
