using System.Diagnostics.CodeAnalysis;

using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Optional execution seam that lets the runtime inspect an idempotency cache
/// before emitting real tool-invocation progress and lifecycle events.
/// </summary>
public interface ICacheAwareToolExecutor
{
    /// <summary>
    /// Attempts to retrieve a previously cached result for the tool invocation.
    /// </summary>
    bool TryGetCachedResult(
        ITool tool,
        ToolInvocation invocation,
        [MaybeNullWhen(false)] out ToolResult result);
}
