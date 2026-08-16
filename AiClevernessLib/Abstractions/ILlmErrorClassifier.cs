using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Classifies LLM completion failures to determine whether the runtime should
/// advance to the next candidate model or abort.
/// </summary>
/// <remarks>
/// Internal extension point. The default implementation classifies per-turn
/// timeouts as <see cref="FailureClassification.TransientAdvance"/>.
/// Rate-limit and unavailable-model signals can be added to a custom
/// implementation without touching the tool loop.
/// </remarks>
internal interface ILlmErrorClassifier
{
    /// <summary>
    /// Classifies the given exception.
    /// </summary>
    /// <param name="exception">The exception thrown during LLM completion.</param>
    /// <param name="callerToken">
    /// The caller-supplied cancellation token. Used to distinguish per-turn
    /// timeouts (internal CTS cancelled) from user-initiated cancellation.
    /// </param>
    FailureClassification Classify(Exception exception, CancellationToken callerToken);
}
