using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Workflows;

/// <summary>
/// Implements a coordinator/reviewer multi-agent pattern.
/// A coordinator agent produces output, then a reviewer agent evaluates it.
/// If the reviewer rejects, the coordinator retries with feedback.
/// </summary>
public sealed class CoordinatorReviewerPattern
{
    private const int DefaultMaxReviewCycles = 3;

    private readonly int _maxReviewCycles;

    private readonly IAgentRuntime _runtime;

    /// <param name="runtime">The agent runtime for executing both agents.</param>
    /// <param name="maxReviewCycles">Maximum number of review-and-retry cycles (default: 3).</param>
    public CoordinatorReviewerPattern(IAgentRuntime runtime, int maxReviewCycles = DefaultMaxReviewCycles)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _maxReviewCycles = maxReviewCycles > 0 ? maxReviewCycles : DefaultMaxReviewCycles;
    }

    /// <summary>
    /// Runs the coordinator/reviewer cycle.
    /// </summary>
    /// <param name="coordinatorGoal">Goal for the coordinator agent.</param>
    /// <param name="reviewerGoalTemplate">
    /// Goal template for the reviewer. Use {{output}} to reference coordinator output.
    /// Reviewer should return "approved" in output if acceptable.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CoordinatorReviewerResult> RunAsync(
        string coordinatorGoal,
        string reviewerGoalTemplate,
        CancellationToken cancellationToken = default)
    {
        var feedback = new List<string>();

        for (var cycle = 0; cycle < _maxReviewCycles; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build coordinator goal with accumulated feedback.
            var fullGoal = feedback.Count > 0
                               ? $"{coordinatorGoal}\n\nPrevious feedback:\n{string.Join("\n", feedback)}"
                               : coordinatorGoal;

            var coordinatorResult = await _runtime.RunAsync(
                                        new AgentRequest(fullGoal),
                                        null,
                                        cancellationToken);

            if (!coordinatorResult.Success)
            {
                return new CoordinatorReviewerResult(
                    false,
                    coordinatorResult.Output,
                    cycle + 1,
                    "Coordinator failed.",
                    feedback);
            }

            // Review the output.
            var reviewGoal = reviewerGoalTemplate.Replace(
                "{{output}}",
                coordinatorResult.Output ?? string.Empty,
                StringComparison.Ordinal);

            var reviewResult = await _runtime.RunAsync(
                                   new AgentRequest(reviewGoal),
                                   null,
                                   cancellationToken);

            if (reviewResult.Output?.Contains("approved", StringComparison.OrdinalIgnoreCase)
                == true)
            {
                return new CoordinatorReviewerResult(
                    true,
                    coordinatorResult.Output,
                    cycle + 1,
                    null,
                    feedback);
            }

            // Reviewer rejected: collect feedback.
            feedback.Add(reviewResult.Output ?? "Reviewer rejected without feedback.");
        }

        return new CoordinatorReviewerResult(
            false,
            null,
            _maxReviewCycles,
            $"Review cycle exhausted after {_maxReviewCycles} attempts.",
            feedback);
    }
}
