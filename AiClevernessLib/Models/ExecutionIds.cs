namespace AiCleverness.Models;

/// <summary>
/// Tracing identifiers attached to an execution for correlation across
/// retries, sub-executions, and distributed observability.
/// </summary>
public sealed record ExecutionIds(
    string ExecutionId,
    string? TraceId = null,
    string? CorrelationId = null)
{
    /// <summary>Creates a new instance with generated identifiers.</summary>
    public static ExecutionIds Create() =>
        new(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

    /// <summary>Creates a new instance as a child of the given parent identifiers.</summary>
    public ExecutionIds CreateChild() =>
        new(
            Guid.NewGuid().ToString("N"),
            TraceId,
            ExecutionId);
}
