namespace AiCleverness.Models;

/// <summary>
/// Lifecycle status of an agent execution.
/// Supersedes the free-form string in <see cref="AgentState.Status"/> for structured tracking.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Execution has been created but not yet started.</summary>
    Pending,

    /// <summary>Execution is actively running.</summary>
    Running,

    /// <summary>Execution completed successfully.</summary>
    Completed,

    /// <summary>Execution completed with an error or quality rejection.</summary>
    Failed,

    /// <summary>Execution was blocked by a policy before running.</summary>
    Blocked,

    /// <summary>Execution timed out during an LLM or tool call.</summary>
    TimedOut,

    /// <summary>Execution was cancelled by the caller.</summary>
    Cancelled
}
