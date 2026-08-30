namespace AiCleverness.Models;

/// <summary>Raised when a tool completes.</summary>
public sealed record ToolCompletedEvent(string ExecutionId, string ToolName, ToolResult Result, TimeSpan Duration, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("ToolCompleted", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
