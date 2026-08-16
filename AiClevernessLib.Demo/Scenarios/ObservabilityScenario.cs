using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows observability: a live observer trace during a run, then aggregate metrics
/// computed by <see cref="DefaultMetricsCollector"/> from the same execution's
/// recorded events (no fictional data — metrics reflect the actual demo run).
///
/// What this shows:
///   - <see cref="ConsoleAgentObserver"/> receives callbacks for every lifecycle
///     event (run start, LLM call, tool invocation, completion) — a consumer can
///     send these to any monitoring system.
///   - <see cref="DefaultMetricsCollector"/> computes aggregate statistics
///     (execution count, success rate, LLM calls, per-tool duration) from
///     recorded execution manifests.
///   - The metrics here are derived from the actual live run — same timings the
///     observer reported. No hardcoded values.
///   - In production, observers feed OpenTelemetry, the metrics collector feeds
///     dashboards.
/// </summary>
internal static class ObservabilityScenario
{
    private const string City = "Oslo";

    private const string ExecutionId = "demo-exec-1";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();
        llm.EnqueueToolCall(WeatherTool.ToolName, $$"""{"city": "{{City}}"}""");
        llm.EnqueueText($"{City} is crisp and clear today.");

        // Collect real execution events via the event bus.
        var eventCollector = new EventCollector();

        await using var scoped = DemoHost.CreateProvider(
            llm,
            services =>
            {
                services.AddAgentObserver<ConsoleAgentObserver>();
                services.AddInMemoryEventBus();
                services.AddSingleton<IExecutionEventHandler<LlmCallCompletedBusEvent>>(eventCollector);
                services.AddSingleton<IExecutionEventHandler<ToolInvokedBusEvent>>(eventCollector);
                services.AddSingleton<IExecutionEventHandler<ToolCompletedBusEvent>>(eventCollector);
            });

        Console.WriteLine("  Live observer trace:");
        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var started = DateTimeOffset.UtcNow;
        var result = await runtime.RunAsync(
            new AgentRequest(
                $"What is the weather in {City}?",
                AllowedToolNames: [WeatherTool.ToolName]));
        var totalDuration = DateTimeOffset.UtcNow - started;
        Console.WriteLine($"  Run finished (success: {result.Success})");
        Console.WriteLine();

        // Build a manifest from the actual run and feed it to the metrics collector.
        var manifest = BuildManifestFromRun(result, totalDuration, eventCollector);

        Console.WriteLine("  Metrics (DefaultMetricsCollector over the actual execution):");
        var metrics = new DefaultMetricsCollector();
        await metrics.RecordAsync(manifest);

        var aggregate = await metrics.GetAggregateMetricsAsync();
        Console.WriteLine($"    executions:       {aggregate.TotalExecutions}");
        Console.WriteLine($"    successful:       {aggregate.SuccessfulExecutions}");
        Console.WriteLine($"    LLM calls:        {aggregate.TotalLlmCalls}");
        Console.WriteLine($"    tool invocations: {aggregate.TotalToolInvocations}");

        foreach (var tool in await metrics.GetToolMetricsAsync())
        {
            Console.WriteLine(
                $"    tool '{tool.ToolName}': {tool.InvocationCount} call(s), " +
                $"avg {(tool.AverageDuration ?? TimeSpan.Zero).TotalMilliseconds:F1} ms");
        }
    }

    private static ExecutionManifest BuildManifestFromRun(
        AgentResult result,
        TimeSpan totalDuration,
        EventCollector collector)
    {
        var request = new AgentRequest($"What is the weather in {City}?");

        var events = new List<ExecutionEvent>
        {
            new ExecutionStartedEvent(ExecutionId, request)
        };

        // Add LLM events from collector.
        foreach (var llmDuration in collector.LlmDurations)
        {
            events.Add(new LlmRespondedEvent(
                ExecutionId,
                new LlmResponse(null),
                llmDuration));
        }

        // Add tool events from collector.
        foreach (var (toolName, toolResult, toolDuration) in collector.ToolCompletions)
        {
            events.Add(new ToolInvokedEvent(
                ExecutionId,
                toolName,
                new ToolInvocation(toolName)));
            events.Add(new ToolCompletedEvent(
                ExecutionId,
                toolName,
                toolResult,
                toolDuration));
        }

        return new ExecutionManifest(
            ExecutionId: ExecutionId,
            TraceId: null,
            CorrelationId: null,
            Status: result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
            CreatedAt: DateTimeOffset.UtcNow,
            Duration: totalDuration,
            Request: request,
            Options: new AgentRuntimeOptions(),
            ToolNames: [WeatherTool.ToolName],
            TurnCount: collector.LlmDurations.Count,
            QualityRetryCount: 0,
            ToolRetryCount: 0,
            Events: events);
    }

    /// <summary>
    /// Collects real execution events from the event bus for metrics.
    /// </summary>
    private sealed class EventCollector :
        IExecutionEventHandler<LlmCallCompletedBusEvent>,
        IExecutionEventHandler<ToolInvokedBusEvent>,
        IExecutionEventHandler<ToolCompletedBusEvent>
    {
        public List<TimeSpan> LlmDurations { get; } = [];

        public List<(string ToolName, ToolResult Result, TimeSpan Duration)> ToolCompletions { get; } = [];

        public Task HandleAsync(LlmCallCompletedBusEvent @event, CancellationToken ct)
        {
            LlmDurations.Add(@event.Duration);
            return Task.CompletedTask;
        }

        public Task HandleAsync(ToolInvokedBusEvent @event, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task HandleAsync(ToolCompletedBusEvent @event, CancellationToken ct)
        {
            ToolCompletions.Add((@event.ToolName, @event.Result, @event.Duration));
            return Task.CompletedTask;
        }
    }
}
