namespace AiCleverness.Abstractions;

/// <summary>
/// Registry for discovering and selecting planners by name or criteria.
/// </summary>
public interface IPlannerRegistry
{
    /// <summary>Gets all planner names.</summary>
    IReadOnlyCollection<string> Names { get; }

    /// <summary>Gets all registered planners.</summary>
    IReadOnlyList<INamedAgentPlanner> GetAll();

    /// <summary>Gets planners matching the given tag.</summary>
    IReadOnlyList<INamedAgentPlanner> GetByTag(string tag);

    /// <summary>Gets a planner by name. Returns null if not found.</summary>
    INamedAgentPlanner? GetPlanner(string name);

    /// <summary>Registers a planner.</summary>
    void Register(INamedAgentPlanner planner);
}
