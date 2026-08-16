using AiClevernessLib.Demo.Scenarios;

namespace AiClevernessLib.Demo;

/// <summary>
/// Hermetic showcase of the AiCleverness runtime: tool loop, streaming, policies,
/// strategies, quality gates, workflows, and observability — no network access.
/// </summary>
public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("=== AiCleverness Demo ===");
        Console.WriteLine();
        Console.WriteLine("NOTE: This demo is fully hermetic — no network access.");
        Console.WriteLine("  - LLM: ScriptedLlmClient (pre-scripted responses, no real AI provider)");
        Console.WriteLine("  - Tools: deterministic fakes (e.g. WeatherTool returns fixed data)");
        Console.WriteLine("  - Purpose: demonstrate the runtime machinery (tool loop, observers,");
        Console.WriteLine("    events, policies, quality gates) — not a real application.");
        Console.WriteLine();

        var llm = new ScriptedLlmClient();
        await using var provider = DemoHost.CreateProvider(llm);

        await RunScenarioAsync(
            "1. Tool loop: scripted LLM decides to call a tool, runtime executes it",
            () => ToolLoopScenario.RunAsync(provider));
        await RunScenarioAsync(
            "2. Streaming: live AgentEvent stream for the same run",
            () => StreamingScenario.RunAsync(provider));
        await RunScenarioAsync(
            "3. Policies: dangerous requests are blocked before any LLM call",
            () => PolicyScenario.RunAsync(provider));
        await RunScenarioAsync(
            "4. Strategies: deterministic shortcut that skips the LLM",
            () => StrategyScenario.RunAsync(provider));
        await RunScenarioAsync(
            "5. Quality gates: weak answers are rejected and retried",
            () => QualityGateScenario.RunAsync(provider));
        await RunScenarioAsync(
            "6. Workflows: two agent nodes run in dependency order",
            () => WorkflowScenario.RunAsync(provider));
        await RunScenarioAsync(
            "7. Observability: observer trace and execution metrics",
            () => ObservabilityScenario.RunAsync(provider));

        Console.WriteLine("=== Demo Complete ===");
    }

    private static async Task RunScenarioAsync(string title, Func<Task> scenario)
    {
        Console.WriteLine($"── {title} ──");
        await scenario();
        Console.WriteLine();
    }
}
