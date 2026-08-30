using System.Net;

namespace AiCleverness.Models;

/// <summary>
/// Provider failure metadata projected into completion diagnostics.
/// </summary>
public sealed record LlmProviderFailureMetadata
{
    /// <summary>Stable provider identifier, when supplied by the adapter.</summary>
    public string? Provider { get; init; }

    /// <summary>Provider-specific error code, when supplied by the adapter.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>HTTP status associated with the provider failure, when available.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>Provider-supplied retry-after duration, when available.</summary>
    public TimeSpan? RetryAfter { get; init; }

    internal static LlmProviderFailureMetadata? FromException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is LlmProviderException providerException)
                return providerException.Metadata;
        }

        return null;
    }
}
