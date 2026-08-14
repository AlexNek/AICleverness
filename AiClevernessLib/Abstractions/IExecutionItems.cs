namespace AiCleverness.Abstractions;

/// <summary>
/// Typed collection for intermediate work items produced during execution.
/// Items are keyed by name and can hold any type, providing a structured
/// alternative to the untyped property bag in <see cref="IAgentContext.Properties"/>.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe.
/// </remarks>
public interface IExecutionItems
{
    /// <summary>Gets the number of items stored.</summary>
    int Count { get; }

    /// <summary>Gets all keys currently stored.</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>Removes all items.</summary>
    void Clear();

    /// <summary>Returns true if an item with the given key exists.</summary>
    bool Contains(string key);

    /// <summary>Gets an item by key, returning default if not found or type mismatch.</summary>
    T? Get<T>(string key);

    /// <summary>Gets an item or adds it using the factory if not present.</summary>
    T GetOrAdd<T>(string key, Func<T> factory);

    /// <summary>Removes an item by key. Returns true if the item existed.</summary>
    bool Remove(string key);

    /// <summary>Sets an item by key.</summary>
    void Set<T>(string key, T value);
}
