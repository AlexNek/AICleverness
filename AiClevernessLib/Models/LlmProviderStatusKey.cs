using System.Net;

namespace AiCleverness.Models;

/// <summary>
/// Case-insensitive key identifying a provider-specific HTTP status policy.
/// </summary>
public readonly record struct LlmProviderStatusKey
{
    /// <summary>
    /// Creates a provider status key.
    /// </summary>
    public LlmProviderStatusKey(string provider, HttpStatusCode statusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        Provider = provider.Trim().ToUpperInvariant();
        StatusCode = statusCode;
    }

    /// <summary>Normalized provider identifier.</summary>
    public string Provider { get; }

    /// <summary>Provider-specific HTTP status.</summary>
    public HttpStatusCode StatusCode { get; }
}
