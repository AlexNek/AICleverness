namespace AiCleverness.Abstractions;

/// <summary>
/// Registry of tools available to agents.
/// </summary>
public interface IToolRegistry
{
    IReadOnlyList<ITool> GetAllTools();

    IReadOnlyList<ITool> GetAvailableTools(IAgentContext context);

    ITool? GetTool(string name);

    void Register(ITool tool);

    void Unregister(string name);
}
