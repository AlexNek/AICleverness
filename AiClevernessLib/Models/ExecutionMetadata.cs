namespace AiCleverness.Models;

/// <summary>
/// Immutable metadata describing a single execution.
/// Set once at creation and never modified during the run.
/// </summary>
public sealed class ExecutionMetadata
{
    /// <summary>Names of tools available for this execution.</summary>
    public IReadOnlyList<string> AvailableToolNames { get; init; } = Array.Empty<string>();

    /// <summary>Correlation identifier linking related executions.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>UTC timestamp when the execution was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Unique execution identifier.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The runtime options in effect for this execution.</summary>
    public required AgentRuntimeOptions Options { get; init; }

    /// <summary>The original request that initiated this execution.</summary>
    public required AgentRequest Request { get; init; }

    /// <summary>Trace identifier for distributed correlation.</summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Creates metadata from an <see cref="ExecutionIds"/> instance and request.
    /// </summary>
    public static ExecutionMetadata Create(
        ExecutionIds ids,
        AgentRequest request,
        AgentRuntimeOptions options,
        IReadOnlyList<string>? availableToolNames = null)
    {
        return new ExecutionMetadata
                   {
                       ExecutionId = ids.ExecutionId,
                       TraceId = ids.TraceId,
                       CorrelationId = ids.CorrelationId,
                       Request = request,
                       Options = options,
                       AvailableToolNames = availableToolNames ?? Array.Empty<string>()
                   };
    }
}
