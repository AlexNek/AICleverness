using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows a policy blocking a dangerous request before any LLM call happens.
/// </summary>
internal static class PolicyScenario
{
    private const string DangerousGoal = "Delete all files in my project folder";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        await using var scoped = DemoHost.CreateProvider(
            llm,
            services => services.AddAgentPolicy<NoDangerousRequestsPolicy>());

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var request = new AgentRequest(DangerousGoal);

        var result = await runtime.RunAsync(request);

        Console.WriteLine($"  Goal:      {request.Goal}");
        Console.WriteLine($"  Success:   {result.Success}");
        Console.WriteLine($"  Reason:    {result.Reasoning}");
        Console.WriteLine(
            $"  LLM calls: {llm.CallCount} (the policy blocked the run before any model call)");
    }
}
