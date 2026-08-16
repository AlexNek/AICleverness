using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates deterministic strategies — fixed answers for known goal patterns
/// that bypass the LLM entirely.
///
/// What this shows:
///   - <see cref="GreetingStrategy"/> matches goals starting with "greet:" and
///     produces a greeting without calling any AI model.
///   - Strategies run before the LLM tool loop. If one matches, the pipeline
///     short-circuits — zero tokens consumed, zero latency from the provider.
///   - In production, use strategies for FAQ answers, lookup tables, cached
///     responses, or any deterministic logic that doesn't need reasoning.
/// </summary>
internal static class StrategyScenario
{
    private const string Recipient = "Alice";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Register the strategy. In production this is done once at startup via DI.
        await using var scoped = DemoHost.CreateProvider(
            llm,
            services => services.AddAgentStrategy<GreetingStrategy>());

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var request = new AgentRequest($"{GreetingStrategy.GoalPrefix}{Recipient}");

        var result = await runtime.RunAsync(request);

        Console.WriteLine($"  Goal:      {request.Goal}");
        Console.WriteLine($"  Success:   {result.Success}");
        Console.WriteLine($"  Output:    {result.Output}");
        Console.WriteLine(
            $"  LLM calls: {llm.CallCount} (the strategy answered without a model)");
    }
}
