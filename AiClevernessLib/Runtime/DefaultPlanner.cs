using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// A simple LLM-based planner that asks the model to break a goal into steps.
/// </summary>
public sealed class DefaultPlanner : INamedAgentPlanner
{
    private readonly ILlmClient _llm;

    private readonly ILogger<DefaultPlanner>? _logger;

    public PlannerMetadata Metadata =>
        new()
            {
                Name = Name,
                Description =
                    "Simple LLM-based planner that decomposes goals into ordered string steps.",
                RequiresLlm = true,
                Tags = ["sequential", "simple"]
            };

    public string Name => "DefaultPlanner";

    public DefaultPlanner(ILlmClient llm, ILogger<DefaultPlanner>? logger = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExecutionPlan> CreatePlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var steps = await PlanAsync(request, context, cancellationToken);
        return new ExecutionPlan { PlannerName = Name, Steps = steps, Goal = request.Goal };
    }

    public async Task<IReadOnlyList<PlannedStep>> PlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt =
            $"You are a planning assistant. Break the following goal into a short, ordered list of concrete steps. "
            +
            $"Respond ONLY with a JSON array of strings. Do not add explanation.\n\nGoal: {request.Goal}";

        var messages = new List<LlmMessage>
                           {
                               new(
                                   LlmMessageRoles.System,
                                   "You produce step-by-step plans as JSON arrays of strings."),
                               new(LlmMessageRoles.User, prompt)
                           };

        try
        {
            var response = await _llm.CompleteAsync(messages, null, null, cancellationToken);
            var content = response.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning("Planner returned empty content.");
                return Array.Empty<PlannedStep>();
            }

            var steps = JsonSerializer.Deserialize(
                content,
                AiClevernessJsonContext.Default.ListString);
            if (steps is null)
            {
                return Array.Empty<PlannedStep>();
            }

            return steps
                .Select((description, index) => new PlannedStep(
                    $"{PlannerVocabulary.StepNamePrefix}{index + 1}",
                    PlannerVocabulary.ActionStepType,
                    description))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse planner output.");
            return Array.Empty<PlannedStep>();
        }
    }
}
