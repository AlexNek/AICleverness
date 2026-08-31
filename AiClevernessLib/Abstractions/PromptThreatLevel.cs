namespace AiCleverness.Abstractions;

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
