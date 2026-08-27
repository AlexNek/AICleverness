using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Creates the minimal shared completion context used by decision-tree failover.</summary>
internal static class DecisionTreeCompletionContextFactory
{
    public static LlmCompletionExecutionContext? Create(DecisionTreeExecutionOptions? options)
    {
        if (options?.EnableModelFailover != true
            || string.IsNullOrWhiteSpace(options.Model)
            || options.ModelFallbackChain is not { Count: > 0 } fallbackChain)
            return null;

        var agentContext = new DefaultAgentContext
        {
            AgentName = "decision-tree",
            Goal = "Decision tree LLM",
            State = new AgentState { Status = "Running" },
            Memory = new InMemoryAgentMemory()
        };
        agentContext.SetProperty(AgentPropertyKeys.EnableModelFailover, true);
        agentContext.SetProperty(AgentPropertyKeys.Model, options.Model);
        agentContext.SetProperty(AgentPropertyKeys.ModelFallbackChain, fallbackChain);

        return new LlmCompletionExecutionContext(
            AgentContext: agentContext,
            RuntimeOptions: new AgentRuntimeOptions { EnableModelFailover = true });
    }
}
