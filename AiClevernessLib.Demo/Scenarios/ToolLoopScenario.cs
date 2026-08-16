using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates the core LLM tool loop — the central feature of the runtime.
///
/// What this shows:
///   1. The LLM receives the user's goal and the list of available tools.
///   2. The LLM decides to call a tool (here: "get_weather" with a city argument).
///   3. The runtime executes the tool and feeds the result back to the LLM.
///   4. The LLM produces a final text answer incorporating the tool's output.
///
/// In production, replace ScriptedLlmClient with a real provider adapter (OpenAI,
/// Anthropic, etc.) and WeatherTool with a real API client. The runtime machinery
/// (tool discovery, execution, message routing) stays identical.
/// </summary>
internal static class ToolLoopScenario
{
    private const string City = "Tokyo";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Script the LLM's behavior for this demo:
        //   Turn 1: LLM asks the runtime to call "get_weather" with city="Tokyo".
        //   Turn 2: LLM receives the tool result and produces a final text answer.
        llm.EnqueueToolCall(WeatherTool.ToolName, $$"""{"city": "{{City}}"}""");
        llm.EnqueueText($"It is pleasant in {City} right now — enjoy your day!");

        var runtime = provider.GetRequiredService<IAgentRuntime>();

        // AllowedToolNames restricts which tools the model can use for this run.
        // The runtime sends only these tool definitions to the LLM.
        var request = new AgentRequest(
            $"What is the weather in {City}?",
            AllowedToolNames: [WeatherTool.ToolName]);

        var result = await runtime.RunAsync(request);

        // Output shows the complete execution: goal, success, final answer,
        // number of LLM round-trips, and each step the runtime performed.
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
