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

/// <summary>
/// Result of a prompt guard evaluation.
/// </summary>
public sealed record PromptGuardResult(
    bool IsSafe,
    string? Reason = null,
    PromptThreatLevel ThreatLevel = PromptThreatLevel.None);

/// <summary>
/// Threat level assigned by a prompt guard.
/// </summary>
public enum PromptThreatLevel
{
    /// <summary>No threat detected.</summary>
    None,

    /// <summary>Suspicious but not conclusive. May log a warning.</summary>
    Low,

    /// <summary>Likely injection or jailbreak attempt.</summary>
    Medium,

    /// <summary>High confidence malicious prompt. Should block execution.</summary>
    High
}
