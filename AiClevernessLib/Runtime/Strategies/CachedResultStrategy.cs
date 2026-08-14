using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Strategies;

/// <summary>
/// Deterministic strategy that returns a cached result for previously seen goals.
/// Useful for avoiding redundant LLM calls for repeated or known queries.
/// </summary>
/// <remarks>
/// This is an example deterministic strategy: no LLM is involved.
/// The cache is keyed by the normalized goal text.
/// </remarks>
public sealed class CachedResultStrategy : IAgentStrategy
{
    private readonly ConcurrentDictionary<string, StrategyResult> _cache = new(
        StringComparer.OrdinalIgnoreCase);

    public string Name => "CachedResult";

    /// <summary>
    /// Pre-populates the cache with a known goal-result mapping.
    /// </summary>
    public void AddEntry(string goal, string output)
    {
        ArgumentNullException.ThrowIfNull(goal);
        _cache[NormalizeKey(goal)] = new StrategyResult(true, output, "Served from cache.");
    }

    /// <inheritdoc/>
    public bool CanExecute(IAgentContext context)
    {
        return _cache.ContainsKey(NormalizeKey(context.Goal));
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <inheritdoc/>
    public Task<StrategyResult> ExecuteAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(NormalizeKey(context.Goal), out var cached))
        {
            return Task.FromResult(cached);
        }

        return Task.FromResult(new StrategyResult(false, null, "Goal not found in cache."));
    }

    /// <summary>
    /// Removes a cached entry.
    /// </summary>
    public bool RemoveEntry(string goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        return _cache.TryRemove(NormalizeKey(goal), out _);
    }

    private static string NormalizeKey(string goal) => goal.Trim();
}
