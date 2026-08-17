namespace AiCleverness.Models;

/// <summary>
/// Categorizes the reason an agent execution failed.
/// Used in <see cref="AgentResult.FailureKind"/> for typed failure assertions
/// without relying on message string content.
/// </summary>
public enum EFailureKind
{
    /// <summary>Not set — the default value when no explicit failure kind has been assigned.</summary>
    None,

    /// <summary>Execution succeeded — no failure occurred.</summary>
    NoFailure,

    /// <summary>LLM call timed out (idle or wall-clock) — model did not respond in time.</summary>
    LlmTimeout,

    /// <summary>LLM call threw a non-timeout exception.</summary>
    LlmError,

    /// <summary>All failover candidates exhausted without a successful response.</summary>
    FailoverExhausted,

    /// <summary>Maximum turn count reached without a final answer.</summary>
    TurnLimitExceeded,

    /// <summary>Execution was cancelled by the caller.</summary>
    Cancelled,

    /// <summary>A policy blocked execution before the LLM was called.</summary>
    PolicyBlocked,

    /// <summary>Input validation failed before execution.</summary>
    InputValidationFailed,

    /// <summary>Unclassified failure — does not match any known failure kind.</summary>
    Unknown
}
