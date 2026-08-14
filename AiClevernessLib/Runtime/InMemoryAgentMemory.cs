using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Simple in-memory implementation of <see cref="IAgentMemory"/>.
/// Suitable for single-run sessions or as a fallback when no persistent memory is configured.
/// </summary>
public sealed class InMemoryAgentMemory : IAgentMemory
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return Task.FromResult(_store.ContainsKey(key));
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToList());
    }

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
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (!_store.TryGetValue(key, out var json))
            return Task.FromResult<T?>(default);

        try
        {
            var value = JsonSerializer.Deserialize<T>(json, AiClevernessJson.CamelCase);
            return Task.FromResult<T?>(value);
        }
        catch (JsonException)
        {
            return Task.FromResult<T?>(default);
        }
    }

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
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var json = JsonSerializer.Serialize(value, AiClevernessJson.CamelCase);
        _store[key] = json;
        return Task.CompletedTask;
    }
}
