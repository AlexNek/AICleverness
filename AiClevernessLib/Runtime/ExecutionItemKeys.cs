namespace AiCleverness.Runtime;

/// <summary>
/// Well-known keys used in <see cref="Abstractions.IExecutionItems"/>.
/// </summary>
internal static class ExecutionItemKeys
{
    /// <summary>
    /// Optional <c>Action&lt;AgentEvent&gt;</c> used to emit streaming events from the
    /// pipeline. Present only during streaming executions; absent for non-streaming runs.
    /// </summary>
    public const string EventEmitter = "eventEmitter";

    /// <summary>Plan produced by the planner, stored as <c>IReadOnlyList&lt;PlannedStep&gt;</c>.</summary>
    public const string Plan = "plan";

    /// <summary>Progress reporter, stored as <c>Action&lt;string&gt;</c>.</summary>
    public const string Progress = "progress";

    /// <summary>Shared step log, stored as <c>List&lt;string&gt;</c>.</summary>
    public const string Steps = "steps";
}
