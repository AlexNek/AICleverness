using System.Net;
using System.Net.Http;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Classifies completion failures for model failover without performing retries.
/// </summary>
internal sealed class DefaultLlmErrorClassifier : ILlmErrorClassifier
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> TransientProviderErrorCodes =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // Anthropic documents overloaded_error as a temporary provider condition.
            ["anthropic"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "overloaded_error"
            },
            // Google/Gemini adapters commonly surface this provider code for capacity.
            ["google"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RESOURCE_EXHAUSTED"
            },
            ["gemini"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RESOURCE_EXHAUSTED"
            },
            // OpenAI's exact rate-limit code is a provider-confirmed transient signal.
            ["openai"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "rate_limit_exceeded"
            }
        };

    private static readonly IReadOnlySet<string> ProvidersSupportingOverload529 =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "anthropic"
        };

    public EFailureClassification Classify(Exception exception, CancellationToken callerToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Caller cancellation is never a failover signal, including when an
        // adapter wraps the cancellation in LlmProviderException.
        if (callerToken.IsCancellationRequested)
            return EFailureClassification.Permanent;

        // An internal timeout is transient only while the caller is alive.
        if (exception is OperationCanceledException)
            return EFailureClassification.TransientAdvance;

        if (exception is LlmProviderException providerException)
        {
            if (providerException.IsTransient == false)
                return EFailureClassification.Permanent;

            if (IsHardPermanentStatus(providerException.StatusCode))
                return EFailureClassification.Permanent;

            if (providerException.IsTransient == true)
                return EFailureClassification.TransientAdvance;

            if (IsMappedProviderCode(providerException))
                return EFailureClassification.TransientAdvance;

            var structuredClassification = ClassifyStatus(
                providerException.StatusCode,
                providerException.Provider);
            if (structuredClassification is not null)
                return structuredClassification.Value;
        }
        else if (exception is HttpRequestException httpException)
        {
            var structuredClassification = ClassifyStatus(httpException.StatusCode, provider: null);
            if (structuredClassification is not null)
                return structuredClassification.Value;
        }

        var legacyStatus = TryGetHttpStatusCode(exception.Message);
        if (legacyStatus is not null)
        {
            var legacyClassification = ClassifyLegacyHttpStatus(legacyStatus.Value);
            if (legacyClassification is not null)
                return legacyClassification.Value;
        }

        if (IsRateLimitMessage(exception.Message))
            return EFailureClassification.TransientAdvance;

        return EFailureClassification.Permanent;
    }

    private static bool IsMappedProviderCode(LlmProviderException exception)
    {
        if (string.IsNullOrWhiteSpace(exception.Provider)
            || string.IsNullOrWhiteSpace(exception.ErrorCode))
            return false;

        return TransientProviderErrorCodes.TryGetValue(
                   exception.Provider.Trim(),
                   out var codes)
               && codes.Contains(exception.ErrorCode.Trim());
    }

    private static EFailureClassification? ClassifyStatus(
        HttpStatusCode? statusCode,
        string? provider)
    {
        if (statusCode is null)
            return null;

        var numericStatus = (int)statusCode.Value;
        if (numericStatus == 408 || numericStatus == 429)
            return EFailureClassification.TransientAdvance;

        if (IsHardPermanentStatus(statusCode))
            return EFailureClassification.Permanent;

        if (numericStatus == 529)
        {
            return provider is not null
                   && ProvidersSupportingOverload529.Contains(provider.Trim())
                ? EFailureClassification.TransientAdvance
                : EFailureClassification.Permanent;
        }

        if (numericStatus is 500 or 502 or 503 or 504)
            return EFailureClassification.TransientAdvance;

        if (numericStatus is >= 400 and <= 499)
            return EFailureClassification.Permanent;

        if (numericStatus is >= 500 and <= 599)
            return EFailureClassification.Permanent;

        return EFailureClassification.Permanent;
    }

    private static EFailureClassification? ClassifyLegacyHttpStatus(int statusCode)
    {
        if (statusCode == 408 || statusCode == 429)
            return EFailureClassification.TransientAdvance;

        // Preserve the released HTTP 5xx message compatibility rule while
        // excluding hard-permanent unsupported-operation statuses.
        if (statusCode is >= 500 and <= 599)
            return IsHardPermanentStatus((HttpStatusCode)statusCode)
                ? EFailureClassification.Permanent
                : EFailureClassification.TransientAdvance;

        if (statusCode is >= 400 and <= 499)
            return EFailureClassification.Permanent;

        return null;
    }

    private static bool IsHardPermanentStatus(HttpStatusCode? statusCode)
    {
        if (statusCode is null)
            return false;

        var numericStatus = (int)statusCode.Value;
        return (numericStatus is >= 400 and <= 499 && numericStatus is not 408 and not 429)
               || numericStatus is 501 or 505;
    }

    private static bool IsRateLimitMessage(string message)
    {
        if (TryGetHttpStatusCode(message) == 429)
            return true;

        // These exact legacy patterns remain supported for existing adapters.
        return message.Contains("Rate limit exceeded", StringComparison.OrdinalIgnoreCase)
               || message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Too many requests", StringComparison.OrdinalIgnoreCase)
               || message.Contains("free-models-per-min", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetHttpStatusCode(string message)
    {
        var searchStart = 0;
        while (searchStart < message.Length)
        {
            var markerIndex = message.IndexOf("HTTP ", searchStart, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return null;

            var codeStart = markerIndex + 5;
            if (codeStart + 3 <= message.Length
                && IsAsciiDigit(message[codeStart])
                && IsAsciiDigit(message[codeStart + 1])
                && IsAsciiDigit(message[codeStart + 2]))
            {
                var codeEnd = codeStart + 3;
                if (codeEnd == message.Length
                    || message[codeEnd] == ':'
                    || char.IsWhiteSpace(message[codeEnd]))
                {
                    return (message[codeStart] - '0') * 100
                           + (message[codeStart + 1] - '0') * 10
                           + message[codeStart + 2] - '0';
                }
            }

            searchStart = markerIndex + 5;
        }

        return null;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
