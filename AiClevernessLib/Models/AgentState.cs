namespace AiCleverness.Models;

/// <summary>
/// Mutable shared state carried through an agent run.
/// </summary>
public sealed class AgentState
{
    public Dictionary<string, object> Data { get; } = new();

    public List<string> History { get; } = new();

    public string Status { get; set; } = "Idle";

    public T? Get<T>(string key)
    {
        return Data.TryGetValue(key, out var value) && value is T typed
                   ? typed
                   : default;
    }

    public void Report(string message)
    {
        History.Add(message);
    }

    public void Set<T>(string key, T value)
    {
        Data[key] = value!;
    }
}
