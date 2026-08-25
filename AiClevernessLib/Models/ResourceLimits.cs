namespace AiCleverness.Models;

/// <summary>
/// Enforcement limits for resource consumption during an execution.
/// When any limit is exceeded, the runtime may halt or throttle the execution.
/// </summary>
public sealed record ResourceLimits
{
    /// <summary>Maximum monetary cost. Null means unlimited.</summary>
    public decimal? MaxCost { get; init; }

    /// <summary>Maximum execution wall-clock duration. Null means unlimited.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>Maximum number of decision-tree node visits. Null means unlimited.</summary>
    public int? MaxNodeVisits { get; init; }

    /// <summary>Maximum number of LLM calls. Null means unlimited.</summary>
    public int? MaxLlmCalls { get; init; }

    /// <summary>Maximum number of tool calls. Null means unlimited.</summary>
    public int? MaxToolCalls { get; init; }

    /// <summary>Maximum total tokens (input + output). Null means unlimited.</summary>
    public int? MaxTotalTokens { get; init; }

    /// <summary>Action to take when a limit is exceeded.</summary>
    public ResourceLimitAction OnExceeded { get; init; } = ResourceLimitAction.Halt;

    /// <summary>No limits (everything unlimited).</summary>
    public static ResourceLimits Unlimited => new();
}

/// <summary>
/// Action to take when a resource limit is exceeded.
/// </summary>
public enum ResourceLimitAction
{
    /// <summary>Immediately halt execution.</summary>
    Halt,

    /// <summary>Log a warning but continue execution.</summary>
    Warn,

    /// <summary>Throttle execution (e.g., add delays between calls).</summary>
    Throttle
}
