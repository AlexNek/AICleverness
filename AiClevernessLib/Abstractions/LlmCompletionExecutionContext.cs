using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Optional execution services and policies supplied to the shared LLM completion boundary.
/// </summary>
public sealed record LlmCompletionExecutionContext(
    IAgentContext? AgentContext = null,
    AgentRuntimeOptions? RuntimeOptions = null,
    IReadOnlyList<ToolDefinition>? Tools = null,
    int CompletionTimeoutSeconds = 60,
    int IdleTimeoutSeconds = 30,
    Action<string>? OnChunk = null,
    Action<AgentEvent>? Emit = null,
    List<string>? Steps = null,
    Action<string>? Report = null);