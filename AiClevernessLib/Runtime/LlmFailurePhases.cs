namespace AiCleverness.Runtime;

/// <summary>
/// Stable failure-phase values emitted through streaming diagnostics.
/// </summary>
internal static class LlmFailurePhases
{
    internal const string LlmCompletion = "LlmCompletion";
    internal const string ModelFailover = "ModelFailover";
}
