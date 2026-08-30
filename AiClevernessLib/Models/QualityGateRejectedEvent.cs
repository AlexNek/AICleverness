namespace AiCleverness.Models;

/// <summary>Raised when a quality gate rejects a result.</summary>
public sealed record QualityGateRejectedEvent(string ExecutionId, string GateName, QualityGateResult GateResult, int RetryCount, string? TraceId = null, string? CorrelationId = null)
    : ExecutionEvent("QualityGateRejected", DateTimeOffset.UtcNow, ExecutionId, TraceId, CorrelationId);
