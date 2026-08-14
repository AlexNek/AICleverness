using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows a quality gate rejecting a weak answer and the runtime retrying the LLM.
/// </summary>
internal static class QualityGateScenario
{
    private const string Goal = "Explain what AiCleverness does";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();
        llm.EnqueueText("No.");
        llm.EnqueueText(
            "AiCleverness orchestrates policies, tools, and quality gates around any LLM provider.");

        await using var scoped = DemoHost.CreateProvider(
            llm,
            services => services.AddAgentQualityGate<MinimumLengthGate>());

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var request = new AgentRequest(
            Goal,
            Parameters: new Dictionary<string, object>
                            {
                                [AgentPropertyKeys.MaxQualityRetries] = 1
                            });

        var result = await runtime.RunAsync(request);

        Console.WriteLine($"  Goal:    {request.Goal}");
        Console.WriteLine($"  Success: {result.Success}");
        Console.WriteLine($"  Output:  {result.Output}");
        Console.WriteLine("  Steps:");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"    - {step}");
        }
    }
}
