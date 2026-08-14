using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Observer that traces agent lifecycle events to the console.
/// </summary>
public sealed class ConsoleAgentObserver : IAgentObserver
{
    private const string Prefix = "  [observer]";

    /// <inheritdoc />
    public Task OnGateRejectedAsync(
        IAgentQualityGate gate,
        QualityGateResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"{Prefix} gate '{gate.Name}' rejected the result (retry {retryCount}): {result.Reason}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnLlmCalledAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} LLM called with {messages.Count} message(s)");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnLlmRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} LLM responded in {duration.TotalMilliseconds:F0} ms");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnPolicyBlockedAsync(
        IAgentPolicy policy,
        PolicyResult result,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} policy '{policy.Name}' blocked the run");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnRunCompletedAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} run completed (success: {result.Success})");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnRunStartedAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} run started — goal: \"{request.Goal}\"");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnToolCompletedAsync(
        ITool tool,
        ToolResult result,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} tool '{tool.Name}' completed in {duration.TotalMilliseconds:F0} ms");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnToolInvokedAsync(ITool tool, ToolInvocation invocation, CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} tool '{tool.Name}' invoked");
        return Task.CompletedTask;
    }
}
