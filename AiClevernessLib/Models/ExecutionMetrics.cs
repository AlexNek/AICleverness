namespace AiCleverness.Models;

/// <summary>
/// Aggregated execution metrics for a time window or individual execution.
/// Provides structured observability data for dashboards, alerts, and diagnostics.
/// </summary>
public sealed record ExecutionMetrics
{
    // ── Duration metrics ──────────────────────────────────────────────

    /// <summary>Average execution duration.</summary>
    public TimeSpan? AverageDuration { get; init; }

    /// <summary>Average LLM call duration.</summary>
    public TimeSpan? AverageLlmDuration { get; init; }

    /// <summary>Average tool execution duration.</summary>
    public TimeSpan? AverageToolDuration { get; init; }

    /// <summary>Executions blocked by policies.</summary>
    public long BlockedExecutions { get; init; }

    /// <summary>Executions that were cancelled.</summary>
    public long CancelledExecutions { get; init; }

    /// <summary>UTC timestamp when this metrics snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional execution ID (null for aggregate metrics).</summary>
    public string? ExecutionId { get; init; }

    /// <summary>Executions that ended in failure.</summary>
    public long FailedExecutions { get; init; }

    /// <summary>Tool invocations that failed.</summary>
    public long FailedToolInvocations { get; init; }

    /// <summary>Maximum observed execution duration.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>Minimum observed execution duration.</summary>
    public TimeSpan? MinDuration { get; init; }

    /// <summary>P50 execution duration (median).</summary>
    public TimeSpan? P50Duration { get; init; }

    /// <summary>P95 execution duration.</summary>
    public TimeSpan? P95Duration { get; init; }

    /// <summary>P99 execution duration.</summary>
    public TimeSpan? P99Duration { get; init; }

    /// <summary>Quality gate pass ratio (0.0 - 1.0). Null if no evaluations.</summary>
    public double? QualityGatePassRate =>
        TotalQualityGateEvaluations > 0
            ? 1.0 - (double)QualityGateRejections / TotalQualityGateEvaluations
            : null;

    /// <summary>Quality gate rejections.</summary>
    public long QualityGateRejections { get; init; }

    /// <summary>Executions that completed successfully.</summary>
    public long SuccessfulExecutions { get; init; }

    // ── Derived metrics ───────────────────────────────────────────────

    /// <summary>Success ratio (0.0 - 1.0). Null if no executions.</summary>
    public double? SuccessRate =>
        TotalExecutions > 0
            ? (double)SuccessfulExecutions / TotalExecutions
            : null;

    /// <summary>Executions that timed out.</summary>
    public long TimedOutExecutions { get; init; }

    /// <summary>Tool failure ratio (0.0 - 1.0). Null if no invocations.</summary>
    public double? ToolFailureRate =>
        TotalToolInvocations > 0
            ? (double)FailedToolInvocations / TotalToolInvocations
            : null;

    /// <summary>Total completion tokens consumed.</summary>
    public long TotalCompletionTokens { get; init; }

    // ── Execution counts ──────────────────────────────────────────────

    /// <summary>Total executions started.</summary>
    public long TotalExecutions { get; init; }

    // ── LLM metrics ───────────────────────────────────────────────────

    /// <summary>Total LLM calls made.</summary>
    public long TotalLlmCalls { get; init; }

    /// <summary>Total prompt tokens consumed.</summary>
    public long TotalPromptTokens { get; init; }

    // ── Quality / Retry metrics ───────────────────────────────────────

    /// <summary>Total quality gate evaluations.</summary>
    public long TotalQualityGateEvaluations { get; init; }

    /// <summary>Total quality retries triggered.</summary>
    public long TotalQualityRetries { get; init; }

    /// <summary>Total tokens consumed (prompt + completion).</summary>
    public long TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    // ── Tool metrics ──────────────────────────────────────────────────

    /// <summary>Total tool invocations.</summary>
    public long TotalToolInvocations { get; init; }

    /// <summary>Total tool retries triggered.</summary>
    public long TotalToolRetries { get; init; }
}

/// <summary>
/// Per-tool breakdown metrics.
/// </summary>
public sealed record ToolMetrics(
    string ToolName,
    long InvocationCount,
    long FailureCount,
    TimeSpan? AverageDuration,
    TimeSpan? MaxDuration)
{
    /// <summary>Tool failure rate (0.0 - 1.0).</summary>
    public double? FailureRate =>
        InvocationCount > 0
            ? (double)FailureCount / InvocationCount
            : null;
}
