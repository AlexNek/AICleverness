namespace AiCleverness.Models;

/// <summary>
/// Result of executing a workflow.
/// </summary>
public sealed record WorkflowResult
{
    /// <summary>Total duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if the workflow failed.</summary>
    public string? Error { get; init; }

    /// <summary>Per-node results keyed by node ID.</summary>
    public IReadOnlyDictionary<string, AgentResult> NodeResults { get; init; } =
        new Dictionary<string, AgentResult>();

    /// <summary>Final output of the workflow.</summary>
    public string? Output { get; init; }

    /// <summary>Whether the workflow completed successfully.</summary>
    public required bool Success { get; init; }
}
