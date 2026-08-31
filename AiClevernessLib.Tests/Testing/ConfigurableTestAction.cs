using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiClevernessLib.Tests.Testing;

internal sealed class ConfigurableTestAction : IDecisionAction
{
    private readonly IReadOnlyList<DecisionActionResult> _results;
    private int _nextResult;

    public ConfigurableTestAction(string key, params DecisionActionResult[] results)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("An action key is required.", nameof(key))
            : key;
        _results = results is { Length: > 0 }
            ? results
            : throw new ArgumentException("At least one action result is required.", nameof(results));
    }

    public string Key { get; }

    public Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default)
    {
        if (_nextResult >= _results.Count)
            throw new InvalidOperationException($"No configured result remains for action '{Key}'.");

        return Task.FromResult(_results[_nextResult++]);
    }
}
