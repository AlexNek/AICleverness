namespace AiCleverness.Abstractions;

/// <summary>
/// Short-term working memory for a single execution.
/// Automatically cleared when the execution completes.
/// Use this for intermediate state that does not need to persist.
/// </summary>
public interface IWorkingMemory
{
    /// <summary>Gets the number of entries.</summary>
    int Count { get; }

    /// <summary>Gets all keys currently stored.</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>Clears all working memory entries.</summary>
    void Clear();

    /// <summary>Checks whether a key exists in working memory.</summary>
    bool Contains(string key);

    /// <summary>Retrieves a value from working memory. Returns default if missing or type mismatch.</summary>
    T? Get<T>(string key);

    /// <summary>Removes an entry by key. Returns true if it existed.</summary>
    bool Remove(string key);

    /// <summary>Stores a value in working memory.</summary>
    void Set<T>(string key, T value);
}
