using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows observability: a live observer trace during a run, then aggregate metrics
/// computed by <see cref="DefaultMetricsCollector"/> from a recorded execution manifest.
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

        await using var scoped = DemoHost.CreateProvider(
            llm,
            services => services.AddAgentObserver<ConsoleAgentObserver>());

        Console.WriteLine("  Live observer trace:");
        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var result = await runtime.RunAsync(
            new AgentRequest($"What is the weather in {City}?", AllowedToolNames: [WeatherTool.ToolName]));
        Console.WriteLine($"  Run finished (success: {result.Success})");
        Console.WriteLine();

        Console.WriteLine("  Metrics (DefaultMetricsCollector over a recorded manifest):");
        var metrics = new DefaultMetricsCollector();
        await metrics.RecordAsync(BuildSampleManifest());

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

    private static ExecutionManifest BuildSampleManifest()
    {
        var request = new AgentRequest($"What is the weather in {City}?");

        return new ExecutionManifest(
            ExecutionId: ExecutionId,
            TraceId: null,
            CorrelationId: null,
            Status: ExecutionStatus.Completed,
            CreatedAt: DateTimeOffset.UtcNow,
            Duration: TimeSpan.FromMilliseconds(850),
            Request: request,
            Options: new AgentRuntimeOptions(),
            ToolNames: [WeatherTool.ToolName],
            TurnCount: 2,
            QualityRetryCount: 0,
            ToolRetryCount: 0,
            Events:
            [
                new ExecutionStartedEvent(ExecutionId, request),
                new LlmRespondedEvent(
                    ExecutionId,
                    new LlmResponse(null),
                    TimeSpan.FromMilliseconds(300)),
                new ToolInvokedEvent(
                    ExecutionId,
                    WeatherTool.ToolName,
                    new ToolInvocation(WeatherTool.ToolName)),
                new ToolCompletedEvent(
                    ExecutionId,
                    WeatherTool.ToolName,
                    new ToolResult(true, "ok"),
                    TimeSpan.FromMilliseconds(120)),
                new LlmRespondedEvent(
                    ExecutionId,
                    new LlmResponse($"{City} is crisp and clear today."),
                    TimeSpan.FromMilliseconds(280))
            ]);
    }
}
