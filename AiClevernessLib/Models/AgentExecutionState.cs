namespace AiCleverness.Models;

/// <summary>
/// Structured lifecycle metadata for a single agent execution.
/// Tracks timing, status transitions, and retry information.
/// </summary>
public sealed class AgentExecutionState
{
    /// <summary>UTC timestamp when execution completed (success or failure).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Correlation identifier shared across related executions.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Unique identifier for this execution.</summary>
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Total quality-gate retry count for this execution.</summary>
    public int QualityRetryCount { get; set; }

    /// <summary>UTC timestamp when execution started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Current lifecycle status.</summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Total tool-call retry count for this execution.</summary>
    public int ToolRetryCount { get; set; }

    /// <summary>Number of LLM turns taken.</summary>
    public int TurnCount { get; set; }
}
