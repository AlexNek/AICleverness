namespace AiCleverness.Models;

/// <summary>Raised when a tool is invoked.</summary>
public sealed record ToolInvokedEvent(string ExecutionId, string ToolName, ToolInvocation Invocation, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("ToolInvoked", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
