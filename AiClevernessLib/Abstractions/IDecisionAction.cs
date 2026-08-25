using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>Application extension that performs work for an action node.</summary>
public interface IDecisionAction
{
    string Name { get; }

    Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default);
}
