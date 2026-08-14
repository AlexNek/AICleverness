namespace AiCleverness.Models;

/// <summary>
/// Mutable execution state tracking lifecycle progress, timing, and counters.
/// Updated as the execution progresses through policies, tool calls, quality gates, etc.
/// </summary>
/// <remarks>
/// This is the structured replacement for the free-form <see cref="AgentState.Status"/> string.
/// Property setters are intentionally public to allow runtime components to update state.
/// </remarks>
public sealed class ExecutionState
{
    private readonly object _lock = new();

    /// <summary>UTC timestamp when execution completed (success or failure).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Elapsed duration since start, or total duration if completed.</summary>
    public TimeSpan? Duration =>
        CompletedAt is not null && StartedAt is not null
            ? CompletedAt.Value - StartedAt.Value
            : StartedAt is not null
                ? DateTimeOffset.UtcNow - StartedAt.Value
                : null;

    /// <summary>Total quality-gate retry count.</summary>
    public int QualityRetryCount { get; set; }

    /// <summary>UTC timestamp when execution started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Current lifecycle status.</summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Free-form status detail (e.g. current step description).</summary>
    public string? StatusDetail { get; set; }

    /// <summary>Number of tool invocations executed.</summary>
    public int ToolInvocationCount { get; set; }

    /// <summary>Total tool-call retry count.</summary>
    public int ToolRetryCount { get; set; }

    /// <summary>Number of LLM turns taken so far.</summary>
    public int TurnCount { get; set; }

    /// <summary>Thread-safe increment of the quality retry counter.</summary>
    public void IncrementQualityRetry()
    {
        lock (_lock)
        {
            QualityRetryCount++;
        }
    }

    /// <summary>Thread-safe increment of the tool invocation counter.</summary>
    public void IncrementToolInvocation()
    {
        lock (_lock)
        {
            ToolInvocationCount++;
        }
    }

    /// <summary>Thread-safe increment of the tool retry counter.</summary>
    public void IncrementToolRetry()
    {
        lock (_lock)
        {
            ToolRetryCount++;
        }
    }

    /// <summary>Thread-safe increment of the turn counter.</summary>
    public void IncrementTurn()
    {
        lock (_lock)
        {
            TurnCount++;
        }
    }

    /// <summary>Marks the execution as completed with the given status.</summary>
    public void MarkCompleted(ExecutionStatus finalStatus)
    {
        Status = finalStatus;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks the execution as started.</summary>
    public void MarkStarted()
    {
        Status = ExecutionStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Converts this mutable state to the existing <see cref="AgentExecutionState"/> snapshot model.
    /// </summary>
    public AgentExecutionState ToSnapshot(string? correlationId = null)
    {
        return new AgentExecutionState
                   {
                       ExecutionId = string.Empty, // Caller supplies from metadata
                       CorrelationId = correlationId,
                       Status = Status,
                       StartedAt = StartedAt,
                       CompletedAt = CompletedAt,
                       QualityRetryCount = QualityRetryCount,
                       ToolRetryCount = ToolRetryCount,
                       TurnCount = TurnCount
                   };
    }
}
