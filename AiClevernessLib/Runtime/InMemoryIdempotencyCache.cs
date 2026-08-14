using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory implementation of <see cref="IIdempotencyCache"/>.
/// Per-execution scoped: entries are prefixed with the execution ID and cleared on completion.
/// </summary>
public sealed class InMemoryIdempotencyCache : IIdempotencyCache
{
    private readonly ConcurrentDictionary<string, ToolResult> _cache = new();

    /// <inheritdoc/>
    public int Count => _cache.Count;

    /// <inheritdoc/>
    public void Clear(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var keysToRemove = _cache.Keys
            .Where(k => k.StartsWith(scope, StringComparison.Ordinal))
            .ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public void Set(string key, ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(result);
        _cache.TryAdd(key, result);
    }

    /// <inheritdoc/>
    public bool TryGet(string key, [MaybeNullWhen(false)] out ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _cache.TryGetValue(key, out result);
    }
}
