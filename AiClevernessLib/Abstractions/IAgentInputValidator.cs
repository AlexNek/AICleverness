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
