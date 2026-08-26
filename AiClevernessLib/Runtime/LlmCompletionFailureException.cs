using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>Signals that the shared completion pipeline exhausted its retry policy.</summary>
internal sealed class LlmCompletionFailureException : Exception
{
    public LlmCompletionFailureException(
        Exception innerException,
        EFailureClassification classification,
        bool timeout,
        bool failoverEnabled)
        : base(innerException.Message, innerException)
    {
        Classification = classification;
        Timeout = timeout;
        FailoverEnabled = failoverEnabled;
    }

    public EFailureClassification Classification { get; }

    public bool FailoverEnabled { get; }

    public bool Timeout { get; }
}