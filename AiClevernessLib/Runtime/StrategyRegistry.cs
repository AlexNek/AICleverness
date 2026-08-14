using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Default in-memory implementation of <see cref="IStrategyRegistry"/>.
/// Thread-safe for concurrent registration and lookup.
/// </summary>
public sealed class StrategyRegistry : IStrategyRegistry
{
    private readonly ConcurrentDictionary<string, IAgentStrategy> _strategies =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Names => _strategies.Keys.ToArray();

    public StrategyRegistry()
    {
    }

    public StrategyRegistry(IEnumerable<IAgentStrategy> strategies)
    {
        foreach (var strategy in strategies)
        {
            Register(strategy);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgentStrategy> GetAll() => _strategies.Values.ToArray();

    /// <inheritdoc/>
    public IReadOnlyList<IAgentStrategy> GetApplicable(IAgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _strategies.Values.Where(s => s.CanExecute(context)).ToList();
    }

    /// <inheritdoc/>
    public IAgentStrategy? GetStrategy(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _strategies.TryGetValue(name, out var strategy) ? strategy : null;
    }

    /// <inheritdoc/>
    public void Register(IAgentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategies[strategy.Name] = strategy;
    }
}
