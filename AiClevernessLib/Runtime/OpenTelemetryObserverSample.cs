using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Sample <see cref="IAgentObserver"/> that demonstrates how to emit OpenTelemetry-compatible
/// spans and events using <see cref="ILogger"/> structured logging.
/// </summary>
/// <remarks>
/// <para>
/// This observer does not depend on the OpenTelemetry SDK. Instead, it emits structured log entries
/// with standard semantic attributes that can be captured by the OpenTelemetry .NET SDK's
/// <c>ILogger</c> instrumentation.
/// </para>
/// <para>
/// To integrate with real OpenTelemetry:
/// <list type="number">
///   <item>Add the <c>OpenTelemetry.Extensions.Hosting</c> and <c>OpenTelemetry.Instrumentation.Runtime</c> packages.</item>
///   <item>Configure <c>ILogger</c> as an OpenTelemetry log exporter.</item>
///   <item>Replace the <c>ILogger</c> calls with <c>ActivitySource.StartActivity</c> for span creation.</item>
/// </list>
/// </para>
/// <para>
/// Semantic conventions used:
/// <list type="bullet">
///   <item><c>gen_ai.system</c> — identifies the AI system (set by the host).</item>
///   <item><c>gen_ai.request.model</c> — the model used for the LLM call.</item>
///   <item><c>gen_ai.usage.input_tokens</c> / <c>gen_ai.usage.output_tokens</c> — token usage.</item>
///   <item><c>ai.agent.execution_id</c> — the execution identifier.</item>
///   <item><c>ai.tool.name</c> — the tool name.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OpenTelemetryObserverSample : IAgentObserver
{
    private readonly ILogger<OpenTelemetryObserverSample> _logger;

    public OpenTelemetryObserverSample(ILogger<OpenTelemetryObserverSample> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task OnGateRejectedAsync(
        IAgentQualityGate gate,
        QualityGateResult result,
        int retryCount,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "AI quality gate rejected: gate={GateName}, reason={Reason}, retry_count={RetryCount}",
            gate.GetType().Name,
            result.Reason,
            retryCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnLlmCalledAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI LLM called: message_count={MessageCount}, system_message={HasSystem}",
            messages.Count,
            messages.Any(m => m.Role == "system"));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnLlmRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI LLM responded: duration_ms={DurationMs}, has_content={HasContent}, tool_calls={ToolCallCount}, input_tokens={InputTokens}, output_tokens={OutputTokens}",
            duration.TotalMilliseconds,
            !string.IsNullOrEmpty(response.Content),
            response.ToolCalls?.Count ?? 0,
            response.Usage?.PromptTokens ?? 0,
            response.Usage?.CompletionTokens ?? 0);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnPolicyBlockedAsync(
        IAgentPolicy policy,
        PolicyResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "AI policy blocked: policy={PolicyName}, reason={Reason}",
            policy.GetType().Name,
            result.Reasoning);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnRunCompletedAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI execution completed: success={Success}, output_length={OutputLength}, steps={StepCount}",
            result.Success,
            result.Output?.Length ?? 0,
            result.Steps.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnRunStartedAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        // null = unrestricted (every registered tool); an explicit list —
        // including an empty one (no tools) — restricts the run.
        var toolSelection = request.AllowedToolNames is null
                                ? "unrestricted"
                                : request.AllowedToolNames.Count == 0
                                    ? "none"
                                    : "named";
        _logger.LogInformation(
            "AI execution started: goal={Goal}, tool_selection={ToolSelection}, tool_count={ToolCount}",
            request.Goal,
            toolSelection,
            request.AllowedToolNames?.Count ?? -1);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnToolCompletedAsync(
        ITool tool,
        ToolResult result,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI tool completed: ai.tool.name={ToolName}, success={Success}, duration_ms={DurationMs}",
            tool.Definition.Name,
            result.Success,
            duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnToolInvokedAsync(
        ITool tool,
        ToolInvocation invocation,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI tool invoked: ai.tool.name={ToolName}, invocation_id={InvocationId}",
            tool.Definition.Name,
            invocation.GetHashCode());
        return Task.CompletedTask;
    }
}
