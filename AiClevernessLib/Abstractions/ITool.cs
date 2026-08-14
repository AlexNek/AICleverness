using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// A capability that performs work for an agent. Tools never decide; they only execute.
/// </summary>
public interface ITool
{
    ToolDefinition Definition { get; }

    string Description { get; }

    string Name { get; }

    Task<ToolResult> InvokeAsync(
        ToolInvocation invocation,
        CancellationToken cancellationToken = default);
}
