using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>
/// Publishable event raised when an execution starts.
/// </summary>
public sealed record ExecutionStartedBusEvent(
    string ExecutionId,
    AgentRequest Request) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ExecutionStarted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when an execution completes (success or failure).
/// </summary>
public sealed record ExecutionCompletedBusEvent(
    string ExecutionId,
    AgentResult Result,
    TimeSpan? Duration) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ExecutionCompleted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a tool invocation begins.
/// </summary>
public sealed record ToolInvokedBusEvent(
    string ExecutionId,
    string ToolName,
    ToolInvocation Invocation) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ToolInvoked";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a tool invocation completes.
/// </summary>
public sealed record ToolCompletedBusEvent(
    string ExecutionId,
    string ToolName,
    ToolResult Result,
    TimeSpan Duration) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ToolCompleted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a tool invocation fails with an exception.
/// </summary>
public sealed record ToolFailedBusEvent(
    string ExecutionId,
    string ToolName,
    string ErrorMessage) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ToolFailed";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a quality gate evaluates a result.
/// </summary>
public sealed record QualityGateEvaluatedBusEvent(
    string ExecutionId,
    string GateName,
    bool Approved,
    bool Retry,
    string? Reason,
    int RetryCount) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "QualityGateEvaluated";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a result validator evaluates a result.
/// </summary>
public sealed record ValidationCompletedBusEvent(
    string ExecutionId,
    string ValidatorName,
    bool IsValid,
    string? Error) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "ValidationCompleted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a result transformer modifies a result.
/// </summary>
public sealed record TransformationCompletedBusEvent(
    string ExecutionId,
    string TransformerName) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "TransformationCompleted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when a policy blocks execution.
/// </summary>
public sealed record PolicyBlockedBusEvent(
    string ExecutionId,
    string PolicyName,
    string? Reason) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "PolicyBlocked";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publishable event raised when an LLM call completes — on every outcome
/// (success, timeout, or error), so attempt metrics see every attempt.
/// </summary>
public sealed record LlmCallCompletedBusEvent(
    string ExecutionId,
    TimeSpan Duration,
    LlmTokenUsage? Usage,
    bool Success = true,
    int Turn = 0,
    string? Error = null) : IExecutionEvent
{
    /// <inheritdoc />
    public string EventType => "LlmCallCompleted";

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
