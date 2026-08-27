namespace AiCleverness.Models.DecisionTree;

/// <summary>Bounded data representation selected for one classification prompt.</summary>
public sealed record DecisionDataSelection(
    IReadOnlyList<DecisionData> Items,
    int OmittedItemCount,
    int TruncatedItemCount,
    int AggregateTruncatedItemCount = 0)
{
    /// <summary>Gets whether no items or content were omitted or truncated.</summary>
    public bool IsComplete =>
        OmittedItemCount == 0
        && TruncatedItemCount == 0
        && AggregateTruncatedItemCount == 0;
}
