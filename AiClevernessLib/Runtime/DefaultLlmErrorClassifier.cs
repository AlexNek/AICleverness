using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Default classifier: per-turn timeout → <see cref="FailureClassification.TransientAdvance"/>;
/// everything else → <see cref="FailureClassification.Permanent"/>.
/// </summary>
/// <remarks>
/// Extension point for future signals:
/// <list type="bullet">
///   <item>HTTP 429 (rate limit) — check inner exception / message for rate-limit indicators</item>
///   <item>HTTP 503 (model unavailable) — provider signals model is temporarily down</item>
/// </list>
/// Add new rules here without touching <see cref="LlmToolLoop"/>.
/// </remarks>
internal sealed class DefaultLlmErrorClassifier : ILlmErrorClassifier
{
    public FailureClassification Classify(Exception exception, CancellationToken callerToken)
    {
        // Per-turn timeout: the internal linked CTS was cancelled, but the
        // caller's token is still alive.
        if (exception is OperationCanceledException && !callerToken.IsCancellationRequested)
        {
            return FailureClassification.TransientAdvance;
        }

        return FailureClassification.Permanent;
    }
}
