using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Observer that traces agent lifecycle events to the console.
///
/// This demonstrates the IAgentObserver interface — the runtime notifies
/// registered observers on every step: run start/end, each LLM call and response,
/// tool invocations, model switches, quality gate rejections, and policy blocks.
///
/// In production, implement IAgentObserver to send events to your monitoring
/// system (OpenTelemetry, Application Insights, Datadog, etc.).
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
    public Task OnLlmCallCompletedAsync(LlmCallInfo info, CancellationToken cancellationToken)
    {
        var status = info.Success ? "success" : $"failed: {info.Error}";
        Console.WriteLine(
            $"{Prefix} LLM call completed — model: {info.Model}, attempt: {info.Attempt}, "
            + $"duration: {info.Duration.TotalMilliseconds:F0} ms, {status}");
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
    public Task OnModelSwitchedAsync(
        string fromModel,
        string toModel,
        string reason,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} model switched: '{fromModel}' → '{toModel}' (reason: {reason})");
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
        var output = result.Success
            ? result.Output ?? "(empty)"
            : $"error: {result.Error}";
        Console.WriteLine(
            $"{Prefix} tool '{tool.Name}' completed in {duration.TotalMilliseconds:F0} ms — {output}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnToolInvokedAsync(ITool tool, ToolInvocation invocation, CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Prefix} tool '{tool.Name}' invoked");
        return Task.CompletedTask;
    }
}
