namespace AiCleverness.Models;

/// <summary>Category of an error for retry classification.</summary>
public enum RetryCategory
{
    Transient,
    ServerError,
    ClientError,
    RateLimited,
    AuthenticationError,
    ValidationError,
    ResourceExhausted,
    Unknown
}
