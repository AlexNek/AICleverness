namespace AiCleverness.Models;

/// <summary>
/// Actual resource usage tracked during an execution.
/// Updated as execution progresses for accounting and enforcement.
/// </summary>
public sealed class ResourceUsage
{
    private readonly object _lock = new();

    /// <summary>Accumulated monetary cost.</summary>
    public decimal Cost { get; private set; }

    /// <summary>Total wall-clock duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Total input tokens consumed so far.</summary>
    public int InputTokens { get; private set; }

    /// <summary>Number of LLM calls made.</summary>
    public int LlmCalls { get; private set; }

    /// <summary>Total output tokens generated so far.</summary>
    public int OutputTokens { get; private set; }

    /// <summary>Number of tool invocations executed.</summary>
    public int ToolCalls { get; private set; }

    /// <summary>Total tokens (input + output).</summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>Checks whether the usage exceeds the given limits.</summary>
    public bool Exceeds(ResourceLimits limits)
    {
        if (limits.MaxTotalTokens.HasValue && TotalTokens > limits.MaxTotalTokens.Value)
            return true;
        if (limits.MaxLlmCalls.HasValue && LlmCalls > limits.MaxLlmCalls.Value) return true;
        if (limits.MaxToolCalls.HasValue && ToolCalls > limits.MaxToolCalls.Value) return true;
        if (limits.MaxCost.HasValue && Cost > limits.MaxCost.Value) return true;
        if (limits.MaxDuration.HasValue && Duration > limits.MaxDuration.Value) return true;
        return false;
    }

    /// <summary>Adds arbitrary cost (e.g., from external APIs).</summary>
    public void RecordCost(decimal cost)
    {
        lock (_lock)
        {
            Cost += cost;
        }
    }

    /// <summary>Records token usage from an LLM response.</summary>
    public void RecordLlmUsage(int inputTokens, int outputTokens, decimal cost = 0)
    {
        lock (_lock)
        {
            InputTokens += inputTokens;
            OutputTokens += outputTokens;
            LlmCalls++;
            Cost += cost;
        }
    }

    /// <summary>Records a tool invocation.</summary>
    public void RecordToolCall(decimal cost = 0)
    {
        lock (_lock)
        {
            ToolCalls++;
            Cost += cost;
        }
    }
}
