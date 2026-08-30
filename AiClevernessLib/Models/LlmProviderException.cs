using System.Net;

namespace AiCleverness.Models;

/// <summary>
/// Provider-neutral structured failure raised by an LLM adapter.
/// </summary>
/// <remarks>
/// This exception carries provider diagnostics and an optional adapter-authenticated
/// transient indication. It does not perform retries or select fallback models.
/// </remarks>
public sealed class LlmProviderException : Exception
{
    /// <summary>
    /// Creates a structured provider failure using the original provider exception
    /// as the inner exception and message source.
    /// </summary>
    public LlmProviderException(
        Exception innerException,
        string? provider = null,
        string? errorCode = null,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        bool? isTransient = null)
        : base(RequireInnerException(innerException).Message, innerException)
    {
        if (retryAfter < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryAfter));

        Provider = provider;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        IsTransient = isTransient;
        Metadata = new LlmProviderFailureMetadata
        {
            Provider = provider,
            ErrorCode = errorCode,
            StatusCode = statusCode,
            RetryAfter = retryAfter
        };
    }

    /// <summary>Stable provider identifier, when available.</summary>
    public string? Provider { get; }

    /// <summary>Provider-specific error code, when available.</summary>
    public string? ErrorCode { get; }

    /// <summary>HTTP status associated with the provider failure, when available.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Provider-supplied retry-after duration, when available.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Adapter-authenticated transient indication. Null delegates to shared rules.
    /// </summary>
    public bool? IsTransient { get; }

    /// <summary>Immutable metadata projection used by diagnostic records.</summary>
    public LlmProviderFailureMetadata Metadata { get; }

    private static Exception RequireInnerException(Exception? exception) =>
        exception ?? throw new ArgumentNullException(nameof(exception));
}
