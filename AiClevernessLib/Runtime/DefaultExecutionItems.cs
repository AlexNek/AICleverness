using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IExecutionItems"/>.
/// Uses a <see cref="ConcurrentDictionary{TKey,TValue}"/> for concurrent access.
/// </summary>
public sealed class DefaultExecutionItems : IExecutionItems
{
    private readonly ConcurrentDictionary<string, object> _items = new();

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Keys => _items.Keys.ToArray();

    /// <inheritdoc/>
    public void Clear() => _items.Clear();

    /// <inheritdoc/>
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.ContainsKey(key);
    }

    /// <inheritdoc/>
    public T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <inheritdoc/>
    public T GetOrAdd<T>(string key, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var result = _items.GetOrAdd(key, _ => factory()!);
        return result is T typed ? typed : default!;
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _items[key] = value;
    }
}
