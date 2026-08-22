using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo.Scenarios;

/// <summary>
/// Demonstrates quality gates — post-execution evaluation that can reject the
/// LLM's answer and force a retry with feedback.
///
/// What this shows:
///   - <see cref="MinimumLengthGate"/> rejects answers shorter than 20 characters.
///   - The LLM's first scripted answer ("No.") is rejected by the gate.
///   - The runtime automatically retries with quality feedback appended to the
///     system prompt, and the LLM's second answer passes.
///   - In production, use gates for JSON schema validation, factual accuracy
///     checks, tone/style enforcement, or safety filtering.
/// </summary>
internal static class QualityGateScenario
{
    private const string Goal = "Explain what AiCleverness does";

    public static async Task RunAsync(
        IServiceProvider provider,
        DemoTranscriptOptions transcriptOptions)
    {
        var llm = provider.GetRequiredService<ScriptedLlmClient>();
        llm.Reset();

        // Script two answers: the first is too short (will be rejected), the second passes.
        llm.EnqueueText("No.");
        llm.EnqueueText(
            "AiCleverness orchestrates policies, tools, and quality gates around any LLM provider.");

        // Register the quality gate.
        await using var scoped = DemoHost.CreateProvider(
            llm,
            services => services.AddAgentQualityGate<MinimumLengthGate>(),
            transcriptOptions);

        var runtime = scoped.GetRequiredService<IAgentRuntime>();
        var request = transcriptOptions.Apply(new AgentRequest(
            Goal,
            Parameters: new Dictionary<string, object>
                        {
                            // Allow one retry when the gate rejects.
                            [AgentPropertyKeys.MaxQualityRetries] = 1
                        }));

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
