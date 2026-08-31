namespace AiCleverness.Models;

/// <summary>
/// Protocol role vocabulary for messages sent to LLM providers.
/// </summary>
internal static class LlmMessageRoles
{
    internal const string System = "system";
    internal const string User = "user";
    internal const string Assistant = "assistant";
    internal const string Tool = "tool";
}
