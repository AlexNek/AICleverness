namespace AiCleverness.Models;

/// <summary>
/// Describes an execution that has been scheduled for future or recurring invocation.
/// </summary>
public sealed record ScheduledExecution
{
    /// <summary>UTC timestamp when this schedule was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this schedule is due to fire now.</summary>
    public bool IsDue => IsEnabled && !IsExpired && DateTimeOffset.UtcNow >= NextRunAt;

    /// <summary>Whether this schedule is currently enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether this schedule has expired (reached max occurrences).</summary>
    public bool IsExpired => MaxOccurrences.HasValue && OccurrenceCount >= MaxOccurrences.Value;

    /// <summary>Optional label for diagnostics and display.</summary>
    public string? Label { get; init; }

    /// <summary>Maximum number of times this schedule should fire. Null for unlimited.</summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>UTC timestamp when the next execution should fire.</summary>
    public required DateTimeOffset NextRunAt { get; init; }

    /// <summary>Number of times this schedule has already fired.</summary>
    public int OccurrenceCount { get; init; }

    /// <summary>
    /// Optional recurrence interval. When set, the execution reschedules
    /// itself after each run by adding this interval to <see cref="NextRunAt"/>.
    /// When <c>null</c>, the execution runs once and is then removed.
    /// </summary>
    public TimeSpan? RecurrenceInterval { get; init; }

    /// <summary>The agent request to execute when the schedule fires.</summary>
    public required AgentRequest Request { get; init; }

    /// <summary>Unique identifier for this scheduled execution.</summary>
    public string ScheduleId { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// Result of a scheduled execution attempt.
/// </summary>
public sealed record ScheduledExecutionResult
{
    /// <summary>UTC timestamp when the scheduled execution completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Error message if the execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>The execution identifier of the triggered run.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The result of the execution, if available.</summary>
    public AgentResult? Result { get; init; }

    /// <summary>The schedule that was fired.</summary>
    public required string ScheduleId { get; init; }

    /// <summary>UTC timestamp when the scheduled execution started.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Whether the execution completed successfully.</summary>
    public bool Success { get; init; }
}
