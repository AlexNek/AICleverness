namespace AiCleverness.Runtime;

/// <summary>
/// Legacy string status vocabulary retained for backward-compatible agent state and transcript output.
/// </summary>
internal static class LegacyExecutionStatuses
{
    internal const string Running = "Running";
    internal const string Completed = "Completed";
    internal const string Blocked = "Blocked";
    internal const string Failed = "Failed";
    internal const string Cancelled = "Cancelled";
    internal const string TurnLimitExceeded = "TurnLimitExceeded";
}
