using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Shows the core tool loop: the scripted LLM asks for a tool call, the runtime
/// executes the tool, feeds the result back to the model, and receives the final answer.
/// </summary>
internal static class ToolLoopScenario
{
    private const string City = "Tokyo";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();
        llm.EnqueueToolCall(WeatherTool.ToolName, $$"""{"city": "{{City}}"}""");
        llm.EnqueueText($"It is pleasant in {City} right now — enjoy your day!");

        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var request = new AgentRequest(
            $"What is the weather in {City}?",
            AllowedToolNames: [WeatherTool.ToolName]);

        var result = await runtime.RunAsync(request);

        Console.WriteLine($"  Goal:      {request.Goal}");
        Console.WriteLine($"  Success:   {result.Success}");
        Console.WriteLine($"  Output:    {result.Output}");
        Console.WriteLine($"  LLM calls: {llm.CallCount}");
        Console.WriteLine("  Steps:");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"    - {step}");
        }
    }
}
