namespace AiCleverness.Abstractions;

/// <summary>
/// Persistent long-term memory that survives across executions.
/// Key-value based with async operations for backing store flexibility.
/// </summary>
/// <remarks>
/// Implementations may use databases, file systems, or cloud storage.
/// The in-memory implementation is suitable for testing or single-process scenarios.
/// </remarks>
public interface ILongTermMemory
{
    /// <summary>Checks whether a key exists.</summary>
    Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets all keys currently stored.</summary>
    Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets all keys matching a prefix.</summary>
    Task<IReadOnlyList<string>> GetKeysAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a value by key. Returns default if not found.</summary>
    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes a value by key. Returns true if it existed.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores a value persistently by key.</summary>
    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
