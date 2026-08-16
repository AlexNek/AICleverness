namespace AiCleverness.Models;

/// <summary>
/// Base record for structured execution events.
/// Subtypes carry event-specific data for batch observation and journaling.
/// </summary>
public abstract record ExecutionEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string ExecutionId,
    string? TraceId = null,
    string? CorrelationId = null);

/// <summary>
/// Raised when an execution starts.
/// </summary>
public sealed record ExecutionStartedEvent(
    string ExecutionId,
    AgentRequest Request,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent(
        "ExecutionStarted",
        DateTimeOffset.UtcNow,
        ExecutionId,
        TraceId,
        CorrelationId);

/// <summary>
/// Raised when an execution completes (success or failure).
/// </summary>
public sealed record ExecutionCompletedEvent(
    string ExecutionId,
    AgentResult Result,
    TimeSpan Duration,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent(
        "ExecutionCompleted",
        DateTimeOffset.UtcNow,
        ExecutionId,
        TraceId,
        CorrelationId);

/// <summary>
/// Raised when an LLM is called.
/// </summary>
public sealed record LlmCalledEvent(
    string ExecutionId,
    IReadOnlyList<LlmMessage> Messages,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent("LlmCalled", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);

/// <summary>
/// Raised when an LLM responds.
/// </summary>
public sealed record LlmRespondedEvent(
    string ExecutionId,
    LlmResponse Response,
    TimeSpan Duration,
    string? TraceId = null,
    string? CorrelationId = null,
    int Turn = 0)
    : ExecutionEvent("LlmResponded", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);

/// <summary>
/// Raised when a tool is invoked.
/// </summary>
public sealed record ToolInvokedEvent(
    string ExecutionId,
    string ToolName,
    ToolInvocation Invocation,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent("ToolInvoked", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);

/// <summary>
/// Raised when a tool completes.
/// </summary>
public sealed record ToolCompletedEvent(
    string ExecutionId,
    string ToolName,
    ToolResult Result,
    TimeSpan Duration,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent("ToolCompleted", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);

/// <summary>
/// Raised when a quality gate rejects a result.
/// </summary>
public sealed record QualityGateRejectedEvent(
    string ExecutionId,
    string GateName,
    QualityGateResult GateResult,
    int RetryCount,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent(
        "QualityGateRejected",
        DateTimeOffset.UtcNow,
        ExecutionId,
        TraceId,
        CorrelationId);

/// <summary>
/// Raised when a policy blocks execution.
/// </summary>
public sealed record PolicyBlockedEvent(
    string ExecutionId,
    string PolicyName,
    PolicyResult PolicyResult,
    string? TraceId = null,
    string? CorrelationId = null)
    : ExecutionEvent("PolicyBlocked", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
