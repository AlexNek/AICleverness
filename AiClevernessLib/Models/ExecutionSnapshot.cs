namespace AiCleverness.Models;

/// <summary>
/// Serializable, provider-neutral snapshot of an execution's full state at a point in time.
/// Designed for JSON persistence so executions can be resumed or inspected offline.
/// </summary>
/// <remarks>
/// <para>
/// This DTO mirrors <see cref="ExecutionMetadata"/> and <see cref="ExecutionState"/>
/// but uses only primitive types and simple collections to ensure round-trip serialization.
/// </para>
/// </remarks>
public sealed record ExecutionSnapshot
{
    /// <summary>Tool names that were available for this execution.</summary>
    public IReadOnlyList<string> AvailableToolNames { get; init; } = Array.Empty<string>();

    /// <summary>UTC timestamp when the snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when execution completed, if it has completed.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Correlation identifier linking related executions.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>UTC timestamp when the execution was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Unique execution identifier.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The original goal that initiated this execution.</summary>
    public required string Goal { get; init; }

    /// <summary>Arbitrary key-value metadata captured at snapshot time.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Total quality-gate retry count.</summary>
    public int QualityRetryCount { get; init; }

    /// <summary>The final result output, if the execution has completed.</summary>
    public string? ResultOutput { get; init; }

    /// <summary>The final result reasoning, if available.</summary>
    public string? ResultReasoning { get; init; }

    /// <summary>Whether the final result was successful.</summary>
    public bool? ResultSuccess { get; init; }

    /// <summary>Schema version for forward-compatible deserialization.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>UTC timestamp when execution started, if it has started.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>Free-form status detail.</summary>
    public string? StatusDetail { get; init; }

    /// <summary>Number of tool invocations executed.</summary>
    public int ToolInvocationCount { get; init; }

    /// <summary>Total tool-call retry count.</summary>
    public int ToolRetryCount { get; init; }

    /// <summary>Trace identifier for distributed correlation.</summary>
    public string? TraceId { get; init; }

    /// <summary>Number of LLM turns taken so far.</summary>
    public int TurnCount { get; init; }

    /// <summary>
    /// Creates a snapshot from live execution metadata and state.
    /// </summary>
    public static ExecutionSnapshot Capture(
        ExecutionMetadata metadata,
        ExecutionState state,
        AgentResult? result = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        return new ExecutionSnapshot
                   {
                       ExecutionId = metadata.ExecutionId,
                       TraceId = metadata.TraceId,
                       CorrelationId = metadata.CorrelationId,
                       CreatedAt = metadata.CreatedAt,
                       Status = state.Status,
                       Goal = metadata.Request.Goal,
                       AvailableToolNames = metadata.AvailableToolNames,
                       StartedAt = state.StartedAt,
                       CompletedAt = state.CompletedAt,
                       TurnCount = state.TurnCount,
                       QualityRetryCount = state.QualityRetryCount,
                       ToolRetryCount = state.ToolRetryCount,
                       ToolInvocationCount = state.ToolInvocationCount,
                       StatusDetail = state.StatusDetail,
                       ResultOutput = result?.Output,
                       ResultReasoning = result?.Reasoning,
                       ResultSuccess = result?.Success,
                       Properties = properties ?? new Dictionary<string, string>()
                   };
    }
}
