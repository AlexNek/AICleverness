using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows a deterministic strategy answering a matching goal without any LLM call.
/// </summary>
internal static class StrategyScenario
{
    private const string Recipient = "Alice";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

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
