using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Default implementation of <see cref="IToolRegistry"/> that stores tools in memory.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ITool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public IReadOnlyList<ITool> GetAvailableTools(IAgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _tools.Values.Where(t => t.Definition is not null).ToList();
    }

    public ITool? GetTool(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _tools.GetValueOrDefault(name);
    }

    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Name] = tool;
    }

    public void Unregister(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _tools.TryRemove(name, out _);
    }
}
