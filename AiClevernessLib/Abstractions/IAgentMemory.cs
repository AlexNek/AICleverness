namespace AiCleverness.Abstractions;

/// <summary>
/// Persistent or ephemeral memory available to an agent during execution.
/// </summary>
public interface IAgentMemory
{
    Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default);

    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
