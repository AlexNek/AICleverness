using System.Globalization;
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
    private readonly LlmFailureClassificationOptions _classificationOptions;

    internal DefaultLlmErrorClassifier(LlmFailureClassificationOptions? classificationOptions = null)
    {
        _classificationOptions = classificationOptions ?? new LlmFailureClassificationOptions();
    }

    private const int ClientErrorMinimum = 400;
    private const int ClientErrorMaximum = 499;
    private const int ServerErrorMinimum = 500;
    private const int ServerErrorMaximum = 599;

    private const string LegacyHttpStatusMarker = "HTTP ";
    private const int LegacyStatusCodeDigitCount = 3;
    private const char AsciiDigitMinimum = '0';
    private const char AsciiDigitMaximum = '9';

    private const string RateLimitExceededPattern = "Rate limit exceeded";
    private const string SnakeCaseRateLimitReachedPattern = "rate_limit reached";
    private const string TooManyRequestsPattern = "Too many requests";
    private const string FreeModelsPerMinutePattern = "free-models-per-min";

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

            if (TryGetConfiguredClassification(providerException, out var configuredClassification))
                return configuredClassification;

            var structuredClassification = ClassifyStatus(providerException.StatusCode);
            if (structuredClassification is not null)
                return structuredClassification.Value;
        }
        else if (exception is HttpRequestException httpException)
        {
            var structuredClassification = ClassifyStatus(httpException.StatusCode);
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

    private bool TryGetConfiguredClassification(
        LlmProviderException exception,
        out EFailureClassification classification)
    {
        if (!string.IsNullOrWhiteSpace(exception.Provider))
        {
            if (!string.IsNullOrWhiteSpace(exception.ErrorCode)
                && _classificationOptions.ProviderErrorMappings.TryGetValue(
                    new LlmProviderErrorKey(exception.Provider, exception.ErrorCode),
                    out classification))
            {
                return true;
            }

            if (exception.StatusCode is not null
                && _classificationOptions.ProviderStatusMappings.TryGetValue(
                    new LlmProviderStatusKey(exception.Provider, exception.StatusCode.Value),
                    out classification))
            {
                return true;
            }
        }

        classification = default;
        return false;
    }

    private static EFailureClassification? ClassifyStatus(
        HttpStatusCode? statusCode)
    {
        if (statusCode is null)
            return null;

        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            return EFailureClassification.TransientAdvance;

        if (IsHardPermanentStatus(statusCode))
            return EFailureClassification.Permanent;

        if (IsTransientServerStatus(statusCode.Value))
            return EFailureClassification.TransientAdvance;

        return EFailureClassification.Permanent;
    }

    private static bool IsTransientServerStatus(HttpStatusCode statusCode) =>
        statusCode is
            HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static EFailureClassification? ClassifyLegacyHttpStatus(int statusCode)
    {
        if (statusCode == (int)HttpStatusCode.RequestTimeout
            || statusCode == (int)HttpStatusCode.TooManyRequests)
        {
            return EFailureClassification.TransientAdvance;
        }

        // Preserve the released HTTP 5xx message compatibility rule while
        // excluding hard-permanent unsupported-operation statuses.
        if (statusCode is >= ServerErrorMinimum and <= ServerErrorMaximum)
        {
            return IsHardPermanentStatus((HttpStatusCode)statusCode)
                ? EFailureClassification.Permanent
                : EFailureClassification.TransientAdvance;
        }

        if (statusCode is >= ClientErrorMinimum and <= ClientErrorMaximum)
            return EFailureClassification.Permanent;

        return null;
    }

    private static bool IsHardPermanentStatus(HttpStatusCode? statusCode)
    {
        if (statusCode is null)
            return false;

        var numericStatus = (int)statusCode.Value;
        return (numericStatus is >= ClientErrorMinimum and <= ClientErrorMaximum
                && statusCode is not HttpStatusCode.RequestTimeout
                && statusCode is not HttpStatusCode.TooManyRequests)
               || statusCode is HttpStatusCode.NotImplemented
                   or HttpStatusCode.HttpVersionNotSupported;
    }

    private static bool IsRateLimitMessage(string message)
    {
        if (TryGetHttpStatusCode(message) == (int)HttpStatusCode.TooManyRequests)
            return true;

        // These exact legacy patterns remain supported for existing adapters.
        return message.Contains(RateLimitExceededPattern, StringComparison.OrdinalIgnoreCase)
               || message.Contains(SnakeCaseRateLimitReachedPattern, StringComparison.OrdinalIgnoreCase)
               || message.Contains(TooManyRequestsPattern, StringComparison.OrdinalIgnoreCase)
               || message.Contains(FreeModelsPerMinutePattern, StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetHttpStatusCode(string message)
    {
        var searchStart = 0;
        while (searchStart < message.Length)
        {
            var markerIndex = message.IndexOf(
                LegacyHttpStatusMarker,
                searchStart,
                StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return null;

            var codeStart = markerIndex + LegacyHttpStatusMarker.Length;
            if (codeStart + LegacyStatusCodeDigitCount <= message.Length
                && IsAsciiDigit(message[codeStart])
                && IsAsciiDigit(message[codeStart + 1])
                && IsAsciiDigit(message[codeStart + 2]))
            {
                var codeEnd = codeStart + LegacyStatusCodeDigitCount;
                if (codeEnd == message.Length
                    || message[codeEnd] == ':'
                    || char.IsWhiteSpace(message[codeEnd]))
                {
                    return int.Parse(
                        message.AsSpan(codeStart, LegacyStatusCodeDigitCount),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture);
                }
            }

            searchStart = codeStart;
        }

        return null;
    }

    private static bool IsAsciiDigit(char value) =>
        value is >= AsciiDigitMinimum and <= AsciiDigitMaximum;
}
