using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Memory;

/// <summary>
/// In-memory implementation of <see cref="IWorkingMemory"/>.
/// Thread-safe for concurrent access within an execution.
/// </summary>
public sealed class InMemoryWorkingMemory : IWorkingMemory
{
    private readonly ConcurrentDictionary<string, object> _store = new();

    /// <inheritdoc/>
    public int Count => _store.Count;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Keys => _store.Keys.ToArray();

    /// <inheritdoc/>
    public void Clear() => _store.Clear();

    /// <inheritdoc/>
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _store.ContainsKey(key);
    }

    /// <inheritdoc/>
    public T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _store.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _store.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _store[key] = value;
    }
}
