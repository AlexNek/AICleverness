using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Performs simple pass/fail validation on a final agent result.
/// </summary>
public interface IAgentResultValidator
{
    string Name { get; }

    Task<ValidationResult> ValidateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken);
}
