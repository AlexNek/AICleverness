using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Result of a prompt guard evaluation.
/// </summary>
public sealed record PromptGuardResult(
    bool IsSafe,
    string? Reason = null,
    PromptThreatLevel ThreatLevel = PromptThreatLevel.None);
