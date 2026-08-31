namespace AiCleverness.Runtime.Workflows;

/// <summary>Result of a coordinator/reviewer execution.</summary>
public sealed record CoordinatorReviewerResult(
    bool Approved,
    string? FinalOutput,
    int CyclesUsed,
    string? Reason = null,
    IReadOnlyList<string>? ReviewerFeedback = null)
{
    public IReadOnlyList<string> ReviewerFeedback { get; init; } = ReviewerFeedback ?? Array.Empty<string>();
}
