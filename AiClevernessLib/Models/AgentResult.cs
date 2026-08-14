namespace AiCleverness.Models;

/// <summary>
/// Output of an agent run.
/// </summary>
public sealed record AgentResult(
    bool Success,
    string? Output = null,
    string? Reasoning = null,
    IReadOnlyList<string>? Steps = null,
    LlmTokenUsage? Usage = null,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        Metadata ?? new Dictionary<string, object>();

    public IReadOnlyList<string> Steps { get; init; } = Steps ?? Array.Empty<string>();
}
