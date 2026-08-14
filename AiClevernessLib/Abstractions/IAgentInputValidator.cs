using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Validates agent input (request, parameters, context) before execution begins.
/// </summary>
public interface IAgentInputValidator
{
    /// <summary>Display name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Validates the agent request and context.
    /// </summary>
    Task<InputValidationResult> ValidateAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of input validation.
/// </summary>
public sealed record InputValidationResult(
    bool IsValid,
    string? Error = null)
{
    /// <summary>A valid result.</summary>
    public static InputValidationResult Valid => new(true);

    /// <summary>Creates an invalid result with error.</summary>
    public static InputValidationResult Invalid(string error) => new(false, error);
}
