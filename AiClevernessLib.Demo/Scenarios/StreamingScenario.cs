using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Runs the same tool-loop request through the streaming entry point and prints
/// each <see cref="AgentEvent"/> as it arrives.
/// </summary>
internal static class StreamingScenario
{
    private const string City = "Berlin";

    public static async Task RunAsync(IServiceProvider provider)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();
        llm.EnqueueToolCall(WeatherTool.ToolName, $$"""{"city": "{{City}}"}""");
        llm.EnqueueText($"In {City} it is cool and windy today.");

        // AgentRuntime implements both entry points; DI registers it as IAgentRuntime.
        var streaming = provider.GetRequiredService<IAgentRuntime>() as IStreamingAgentRuntime
            ?? throw new InvalidOperationException("The registered runtime does not support streaming.");

        var request = new AgentRequest(
            $"What is the weather in {City}?",
            AllowedToolNames: [WeatherTool.ToolName]);

        await foreach (var agentEvent in streaming.RunStreamingAsync(request))
        {
            Console.WriteLine($"  {agentEvent.EventType,-14} {Describe(agentEvent)}");
        }
    }

    private static string Describe(AgentEvent agentEvent) => agentEvent switch
    {
        RunStartedEvent started => $"goal: \"{started.Request.Goal}\"",
        TurnStartedEvent turn => $"turn {turn.Turn}",
        ToolStartedEvent tool => $"invoking '{tool.ToolName}'",
        ToolCompletedAgentEvent tool => $"'{tool.ToolName}' -> {tool.Result.Output}",
        ModelChunkEvent chunk when chunk.IsFinal => chunk.Content,
        RunCompletedEvent completed => $"success: {completed.Result.Success}",
        PolicyBlockedAgentEvent blocked => $"policy '{blocked.PolicyName}'",
        QualityGateAgentEvent gate => $"gate '{gate.GateName}' approved: {gate.Approved}",
        FailureEvent failure => failure.Error,
        CancellationEvent => "cancelled",
        _ => string.Empty
    };
}
