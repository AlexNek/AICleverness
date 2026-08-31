using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>Publishable event raised when a quality gate evaluates a result.</summary>
public sealed record QualityGateEvaluatedBusEvent(string ExecutionId, string GateName, bool Approved, bool Retry, string? Reason, int RetryCount) : IExecutionEvent
{
    public string EventType => "QualityGateEvaluated";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
