using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Builds the <see cref="ToolExecutionPolicy"/> for a tool invocation by resolving
/// retry count, timeout, logging, and metrics settings from agent context properties
/// with fallback to runtime options.
/// </summary>
internal static class ToolPolicyFactory
{
    /// <summary>
    /// Creates the execution policy for invoking <paramref name="tool"/>.
    /// </summary>
    public static ToolExecutionPolicy Create(
        ITool tool,
        IAgentContext context,
        AgentRuntimeOptions options)
    {
        var timeoutSeconds = context.GetProperty<int?>(AgentPropertyKeys.ToolTimeoutSeconds);
        var timeout = timeoutSeconds.HasValue
                          ? TimeSpan.FromSeconds(timeoutSeconds.Value)
                          : tool.Definition.DefaultTimeout
                            ?? (options.DefaultToolTimeoutSeconds.HasValue
                                    ? TimeSpan.FromSeconds(options.DefaultToolTimeoutSeconds.Value)
                                    : null);

        return new ToolExecutionPolicy(
            context.GetProperty<int?>(AgentPropertyKeys.ToolMaxRetries)
            ?? options.DefaultToolMaxRetries,
            timeout,
            context.GetProperty<bool?>(AgentPropertyKeys.ToolLoggingEnabled)
            ?? options.DefaultToolLoggingEnabled,
            context.GetProperty<bool?>(AgentPropertyKeys.ToolMetricsEnabled)
            ?? options.DefaultToolMetricsEnabled);
    }
}
