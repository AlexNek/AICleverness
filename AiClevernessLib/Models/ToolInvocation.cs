namespace AiCleverness.Models;

/// <summary>
/// A request to invoke a tool with arguments.
/// </summary>
public sealed record ToolInvocation(
    string ToolName,
    IReadOnlyDictionary<string, object>? Arguments = null,
    string? InvocationId = null)
{
    public IReadOnlyDictionary<string, object> Arguments { get; init; } =
        Arguments ?? new Dictionary<string, object>();
}
