namespace AiCleverness.Models;

/// <summary>Describes a request to replay a previously recorded execution.</summary>
public sealed record ReplayRequest
{
    public required string ExecutionId { get; init; }
    public string? FromCheckpointId { get; init; }
    public string? OverrideGoal { get; init; }
    public IReadOnlyList<string>? OverrideToolNames { get; init; }
    public bool UseOriginalParameters { get; init; } = true;
}
