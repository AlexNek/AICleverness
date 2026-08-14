using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Default in-memory implementation of <see cref="IPlannerRegistry"/>.
/// Thread-safe for concurrent registration and lookup.
/// </summary>
public sealed class PlannerRegistry : IPlannerRegistry
{
    private readonly ConcurrentDictionary<string, INamedAgentPlanner> _planners =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Names => _planners.Keys.ToArray();

    public PlannerRegistry()
    {
    }

    public PlannerRegistry(IEnumerable<INamedAgentPlanner> planners)
    {
        foreach (var planner in planners)
        {
            Register(planner);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<INamedAgentPlanner> GetAll() => _planners.Values.ToArray();

    /// <inheritdoc/>
    public IReadOnlyList<INamedAgentPlanner> GetByTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return _planners.Values
            .Where(p => p.Metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public INamedAgentPlanner? GetPlanner(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _planners.TryGetValue(name, out var planner) ? planner : null;
    }

    /// <inheritdoc/>
    public void Register(INamedAgentPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(planner);
        _planners[planner.Name] = planner;
    }
}
