namespace AiCleverness.Models;

/// <summary>
/// Classifies an error for retry decisions.
/// </summary>
public sealed record RetryClassification(
    RetryCategory Category,
    bool ShouldRetry,
    TimeSpan? SuggestedDelay = null,
    int? MaxRetries = null,
    string? Reason = null);

/// <summary>
/// Category of an error for retry classification.
/// </summary>
public enum RetryCategory
{
    /// <summary>Transient error (network timeout, rate limit). Safe to retry.</summary>
    Transient,

    /// <summary>Server error (5xx). May be transient.</summary>
    ServerError,

    /// <summary>Client error (4xx except rate limit). Retrying won't help.</summary>
    ClientError,

    /// <summary>Rate limit hit. Should retry after delay.</summary>
    RateLimited,

    /// <summary>Authentication/authorization failure. Retrying won't help without new credentials.</summary>
    AuthenticationError,

    /// <summary>Input validation error. Retrying with same input won't help.</summary>
    ValidationError,

    /// <summary>Resource exhaustion (budget, quota). Cannot retry.</summary>
    ResourceExhausted,

    /// <summary>Unknown/unclassified error.</summary>
    Unknown
}
