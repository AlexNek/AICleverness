using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Shared execution context passed through every step of an agent run.
/// </summary>
public interface IAgentContext
{
    string AgentName { get; }

    string Goal { get; }

    IAgentMemory Memory { get; }

    IReadOnlyDictionary<string, object> Properties { get; }

    AgentState State { get; }

    T? GetProperty<T>(string key);

    void SetProperty<T>(string key, T value);
}
