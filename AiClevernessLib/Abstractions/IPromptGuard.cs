using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Guards against prompt injection, jailbreaks, or other unsafe inputs
/// before they reach the LLM.
/// </summary>
public interface IPromptGuard
{
    /// <summary>Display name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the messages about to be sent to the LLM.
    /// Returns a result indicating whether the prompt is safe.
    /// </summary>
    Task<PromptGuardResult> EvaluateAsync(
        IReadOnlyList<LlmMessage> messages,
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
