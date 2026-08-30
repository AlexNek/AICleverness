namespace AiCleverness.Models;

/// <summary>
/// Case-insensitive key identifying a provider-specific error code.
/// </summary>
public readonly record struct LlmProviderErrorKey
{
    /// <summary>
    /// Creates a provider error-code key.
    /// </summary>
    public LlmProviderErrorKey(string provider, string errorCode)
    {
        Provider = Normalize(provider, nameof(provider));
        ErrorCode = Normalize(errorCode, nameof(errorCode));
    }

    /// <summary>Normalized provider identifier.</summary>
    public string Provider { get; }

    /// <summary>Normalized provider error code.</summary>
    public string ErrorCode { get; }

    private static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim().ToUpperInvariant();
    }
}
