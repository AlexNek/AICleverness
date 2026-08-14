using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Executes tools and owns cross-cutting behavior such as timeout and retries.
/// </summary>
public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolExecutionPolicy policy,
        CancellationToken cancellationToken);
}
