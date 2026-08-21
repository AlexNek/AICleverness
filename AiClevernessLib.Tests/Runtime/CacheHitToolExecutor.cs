using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Runtime;

internal sealed class CacheHitToolExecutor : IToolExecutor, ICacheAwareToolExecutor
{
    public bool ExecuteCalled { get; private set; }

    public bool TryGetCachedResult(
        ITool tool,
        ToolInvocation invocation,
        out ToolResult result)
    {
        result = new ToolResult(true, "cached output");
        return true;
    }

    public Task<ToolResult> ExecuteAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        ExecuteCalled = true;
        throw new InvalidOperationException("A cache hit must not execute the tool.");
    }
}
