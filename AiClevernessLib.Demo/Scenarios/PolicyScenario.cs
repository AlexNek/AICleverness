using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates the policy pipeline — pre-execution guardrails that can block
/// a request before any LLM call happens.
///
/// What this shows:
///   - <see cref="NoDangerousRequestsPolicy"/> inspects the goal text and blocks
///     anything containing "delete".
///   - The LLM is never called (CallCount stays 0) — policies run first in the
///     pipeline and can reject cheaply.
///   - In production, use policies for rate limiting, content filtering, cost
///     budgets, or any business rule that should short-circuit execution.
/// </summary>
internal static class PolicyScenario
{
    private const string DangerousGoal = "Delete all files in my project folder";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Register the policy. In production this is done once at startup via DI.
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
