using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// A reusable strategy for accomplishing a specific task within the agent context.
/// </summary>
public interface IAgentStrategy
{
    string Name { get; }

    bool CanExecute(IAgentContext context);

    Task<StrategyResult> ExecuteAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default);
}
