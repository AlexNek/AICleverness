using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Memory;

/// <summary>
/// In-memory implementation of <see cref="ILongTermMemory"/>.
/// Suitable for testing or single-process scenarios where persistence is not needed.
/// </summary>
public sealed class InMemoryLongTermMemory : ILongTermMemory
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <inheritdoc/>
    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToList());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetKeysAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var keys = _store.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "User-provided types are the caller's responsibility.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "User-provided types are the caller's responsibility.")]
    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_store.TryGetValue(key, out var json))
            return Task.FromResult<T?>(default);

        try
        {
            var value = JsonSerializer.Deserialize<T>(json, AiClevernessJson.CamelCase);
            return Task.FromResult(value);
        }
        catch (JsonException)
        {
            return Task.FromResult<T?>(default);
        }
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(_store.TryRemove(key, out _));
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "User-provided types are the caller's responsibility.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "User-provided types are the caller's responsibility.")]
    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, AiClevernessJson.CamelCase);
        _store[key] = json;
        return Task.CompletedTask;
    }
}
