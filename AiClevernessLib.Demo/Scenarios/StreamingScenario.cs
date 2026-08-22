using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates streaming execution — the same tool-loop run as scenario 1,
/// but using <see cref="IStreamingAgentRuntime"/> which exposes each event
/// as it occurs via <c>IAsyncEnumerable&lt;AgentEvent&gt;</c>.
///
/// What this shows:
///   - Real-time visibility into every runtime step (turn starts, tool calls,
///     model text chunks, completion).
///   - A consumer (e.g. a chat UI) can display partial progress without waiting
///     for the full run to finish.
///   - Each event carries the execution ID so concurrent runs can be multiplexed.
///
/// In production, pipe these events to a WebSocket, SSE stream, or SignalR hub.
/// </summary>
internal static class StreamingScenario
{
    private const string City = "Berlin";

    public static async Task RunAsync(
        IServiceProvider provider,
        DemoTranscriptOptions transcriptOptions)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Same two-turn script: tool call → final text.
        llm.EnqueueToolCall(WeatherTool.ToolName, $$"""{"city": "{{City}}"}""");
        llm.EnqueueText($"In {City} it is cool and windy today.");

        // AgentRuntime implements both IAgentRuntime and IStreamingAgentRuntime.
        var streaming = provider.GetRequiredService<IAgentRuntime>() as IStreamingAgentRuntime
            ?? throw new InvalidOperationException("The registered runtime does not support streaming.");

        var request = transcriptOptions.Apply(new AgentRequest(
            $"What is the weather in {City}?",
            AllowedToolNames: [WeatherTool.ToolName]));

        // Each iteration yields one AgentEvent. The pattern-match below shows
        // how a consumer would handle the most common event types.
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
        ModelSwitchedAgentEvent switched => $"model switched: '{switched.From}' → '{switched.To}'",
        RunCompletedEvent completed => $"success: {completed.Result.Success}",
        PolicyBlockedAgentEvent blocked => $"policy '{blocked.PolicyName}'",
        QualityGateAgentEvent gate => $"gate '{gate.GateName}' approved: {gate.Approved}",
        FailureEvent failure => failure.Error,
        CancellationEvent => "cancelled",
        _ => string.Empty
    };
}
