using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// A planner that produces a sequential execution plan by asking the LLM
/// to decompose a goal into ordered steps with tool assignments.
/// </summary>
/// <remarks>
/// Unlike <see cref="DefaultPlanner"/> which returns simple string steps,
/// this planner produces structured <see cref="PlannedStep"/> entries with
/// tool names and parameters where the LLM can identify them.
/// </remarks>
public sealed class SequentialPlanner : INamedAgentPlanner
{
    private readonly ILlmClient _llm;

    private readonly ILogger<SequentialPlanner>? _logger;

    private readonly IToolRegistry _tools;

    public PlannerMetadata Metadata =>
        new()
            {
                Name = Name,
                Description =
                    "LLM-based planner that decomposes goals into ordered sequential steps with tool assignments.",
                RequiresLlm = true,
                Tags = ["sequential", "tool-aware"]
            };

    public string Name => "SequentialPlanner";

    public SequentialPlanner(
        ILlmClient llm,
        IToolRegistry tools,
        ILogger<SequentialPlanner>? logger = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlannedStep>> PlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var availableTools = _tools.GetAvailableTools(context);
        var toolList = string.Join(", ", availableTools.Select(t => $"{t.Name}: {t.Description}"));

        var systemPrompt = """
                           You are a planning assistant. Given a goal and available tools, produce an ordered plan.
                           Respond ONLY with a JSON array of objects. Each object has:
                           - "name": short step identifier
                           - "type": either "tool" (uses a tool) or "action" (general action)
                           - "description": what this step does
                           - "tool": tool name if type is "tool", otherwise omit
                           Do not add any explanation outside the JSON array.
                           """;

        var userPrompt = $"Goal: {request.Goal}\n\nAvailable tools: {toolList}";

        var messages = new List<LlmMessage>
                           {
                               new("system", systemPrompt), new("user", userPrompt)
                           };

        try
        {
            var response = await _llm.CompleteAsync(messages, null, null, cancellationToken);
            var content = response.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning("SequentialPlanner returned empty content.");
                return Array.Empty<PlannedStep>();
            }

            return ParseSteps(content);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "SequentialPlanner failed to produce plan.");
            return Array.Empty<PlannedStep>();
        }
    }

    private IReadOnlyList<PlannedStep> ParseSteps(string content)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var steps = new List<PlannedStep>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var name = element.GetProperty("name").GetString() ?? $"step-{steps.Count + 1}";
                var type = element.TryGetProperty("type", out var typeProp)
                               ? typeProp.GetString() ?? "action"
                               : "action";
                var description = element.TryGetProperty("description", out var descProp)
                                      ? descProp.GetString()
                                      : null;

                var parameters = new Dictionary<string, object>();
                if (element.TryGetProperty("tool", out var toolProp)
                    && toolProp.GetString() is string toolName)
                {
                    parameters["tool"] = toolName;
                }

                steps.Add(new PlannedStep(name, type, description, parameters));
            }

            return steps;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse SequentialPlanner output as JSON array.");
            return Array.Empty<PlannedStep>();
        }
    }
}
