using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Abstractions;

/// <summary>
/// Selects and bounds generic decision data for one classification prompt.
/// Custom implementations are responsible for returning a bounded representation;
/// the executor exposes the result to builders through a read-only snapshot.
/// </summary>
public interface IDecisionDataPolicy
{
    DecisionDataSelection Select(
        IReadOnlyList<DecisionData> data,
        DecisionDataSelectionContext context);
}
