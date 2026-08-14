using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Default tool executor with timeout and retry handling.
/// </summary>
public sealed class DefaultToolExecutor : IToolExecutor
{
    private readonly ILogger<DefaultToolExecutor>? _logger;

    public DefaultToolExecutor(ILogger<DefaultToolExecutor>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(policy);

        var attempts = Math.Max(0, policy.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var timeoutCts = CreateTimeoutToken(policy.Timeout, cancellationToken);
                return await tool.InvokeAsync(invocation, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                     && policy.Timeout.HasValue)
            {
                if (attempt >= attempts)
                    return new ToolResult(
                        false,
                        null,
                        $"Tool timed out after {policy.Timeout.Value}.");

                _logger?.LogWarning(
                    "Tool {ToolName} timed out on attempt {Attempt}",
                    tool.Name,
                    attempt);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= attempts)
                {
                    _logger?.LogError(ex, "Tool {ToolName} failed on final attempt", tool.Name);
                    return new ToolResult(false, null, $"Exception: {ex.Message}");
                }

                _logger?.LogWarning(
                    ex,
                    "Tool {ToolName} failed on attempt {Attempt}",
                    tool.Name,
                    attempt);
            }
        }

        return new ToolResult(false, null, "Tool execution failed.");
    }

    private static CancellationTokenSource CreateTimeoutToken(
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue)
            timeoutCts.CancelAfter(timeout.Value);

        return timeoutCts;
    }
}
