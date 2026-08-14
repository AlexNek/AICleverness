using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Deterministic strategy that answers greeting goals without calling an LLM.
/// </summary>
public sealed class GreetingStrategy : IAgentStrategy
{
    /// <summary>Goal prefix handled by this strategy (e.g. "greet:Alice").</summary>
    public const string GoalPrefix = "greet:";

    public string Name => "greeting-strategy";

    public bool CanExecute(IAgentContext context) =>
        context.Goal.StartsWith(GoalPrefix, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<StrategyResult> ExecuteAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var recipient = context.Goal[GoalPrefix.Length..].Trim();
        var output =
            $"Hello, {recipient}! This answer came from a deterministic strategy — no LLM was called.";

        return Task.FromResult(new StrategyResult(true, output, "Goal matched the greeting rule."));
    }
}
