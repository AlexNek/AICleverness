using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Strategies;

/// <summary>
/// Deterministic strategy that evaluates rules against the context and returns a result
/// for the first matching rule. No LLM is involved.
/// </summary>
/// <remarks>
/// Rules are evaluated in registration order. The first rule whose predicate returns true
/// produces the strategy output. If no rule matches, the strategy reports that it cannot execute.
/// </remarks>
public sealed class RuleBasedStrategy : IAgentStrategy
{
    private readonly List<(Func<IAgentContext, bool> Predicate, Func<IAgentContext, string> Output)>
        _rules = new();

    public string Name { get; }

    /// <summary>
    /// Creates a new rule-based strategy with the given name.
    /// </summary>
    public RuleBasedStrategy(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Adds a simple rule matching a goal prefix.
    /// </summary>
    public RuleBasedStrategy AddGoalPrefixRule(string prefix, string output)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return AddRule(
            ctx => ctx.Goal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            _ => output);
    }

    /// <summary>
    /// Adds a rule. Rules are evaluated in the order they are added.
    /// </summary>
    /// <param name="predicate">Condition under which this rule applies.</param>
    /// <param name="outputFactory">Factory producing the output when the rule matches.</param>
    public RuleBasedStrategy AddRule(
        Func<IAgentContext, bool> predicate,
        Func<IAgentContext, string> outputFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(outputFactory);
        _rules.Add((predicate, outputFactory));
        return this;
    }

    /// <inheritdoc/>
    public bool CanExecute(IAgentContext context)
    {
        return _rules.Any(r => r.Predicate(context));
    }

    /// <inheritdoc/>
    public Task<StrategyResult> ExecuteAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var (predicate, output) in _rules)
        {
            if (predicate(context))
            {
                return Task.FromResult(
                    new StrategyResult(
                        true,
                        output(context),
                        $"Matched rule in strategy '{Name}'."));
            }
        }

        return Task.FromResult(new StrategyResult(false, null, "No rule matched."));
    }
}
