using System.Diagnostics.CodeAnalysis;

using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Cache for tool invocation results to prevent duplicate execution of side-effecting tools
/// during quality-gate retries or re-execution scenarios.
/// </summary>
public interface IIdempotencyCache
{
    /// <summary>Gets the number of cached entries.</summary>
    int Count { get; }

    /// <summary>Clears all entries matching the given scope prefix.</summary>
    void Clear(string scope);

    /// <summary>Stores a result in the cache. Only successful results should be cached.</summary>
    void Set(string key, ToolResult result);

    /// <summary>Attempts to get a cached result for the given key.</summary>
    bool TryGet(string key, [MaybeNullWhen(false)] out ToolResult result);
}
