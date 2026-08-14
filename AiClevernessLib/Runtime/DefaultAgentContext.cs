using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Default implementation of <see cref="IAgentContext"/>.
/// </summary>
public sealed class DefaultAgentContext : IAgentContext
{
    private readonly ConcurrentDictionary<string, object> _properties = new();

    public string AgentName { get; init; } = "default";

    public required string Goal { get; init; }

    public required IAgentMemory Memory { get; init; }

    public IReadOnlyDictionary<string, object> Properties => _properties;

    public required AgentState State { get; init; }

    public T? GetProperty<T>(string key)
    {
        return _properties.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    public void SetProperty<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _properties[key] = value;
    }
}
