using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Default classifier: per-turn timeout, HTTP 5xx server errors, and HTTP 429
/// rate limits → <see cref="EFailureClassification.TransientAdvance"/>;
/// everything else → <see cref="EFailureClassification.Permanent"/>.
/// </summary>
/// <remarks>
/// Extension point: add new transient-signal rules here without touching
/// <see cref="LlmToolLoop"/>.
/// </remarks>
internal sealed class DefaultLlmErrorClassifier : ILlmErrorClassifier
{
    public EFailureClassification Classify(Exception exception, CancellationToken callerToken)
    {
        // Per-turn timeout: the internal linked CTS was cancelled, but the
        // caller's token is still alive.
        if (exception is OperationCanceledException && !callerToken.IsCancellationRequested)
        {
            return EFailureClassification.TransientAdvance;
        }

        if (IsServerError(exception))
        {
            return EFailureClassification.TransientAdvance;
        }

        if (IsRateLimitError(exception))
        {
            return EFailureClassification.TransientAdvance;
        }

        return EFailureClassification.Permanent;
    }

    /// <summary>
    /// Matches HTTP 5xx server errors based on the status code prefix
    /// embedded in the exception message by AIProviderConnect.
    /// </summary>
    private static bool IsServerError(Exception exception)
    {
        return exception.Message.Contains("HTTP 5", StringComparison.Ordinal);
    }

    /// <summary>
    /// Matches rate-limit errors via HTTP status code or common textual patterns
    /// found in provider error responses.
    /// </summary>
    private static bool IsRateLimitError(Exception exception)
    {
        var message = exception.Message;

        // HTTP 429 prefix from AIProviderConnect (case-sensitive, always uppercase).
        if (message.Contains("HTTP 429", StringComparison.Ordinal))
        {
            return true;
        }

        // Textual rate-limit patterns — case-insensitive since provider casing varies.
        if (message.Contains("Rate limit exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("Too many requests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // OpenRouter-specific rate-limit pattern from response body.
        if (message.Contains("free-models-per-min", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
