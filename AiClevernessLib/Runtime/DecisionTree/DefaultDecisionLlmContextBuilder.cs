using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Builds a deterministic system/user prompt for classify nodes.</summary>
public sealed class DefaultDecisionLlmContextBuilder : IDecisionLlmContextBuilder
{
    public IReadOnlyList<LlmMessage> Build(
        DecisionTreeModel tree,
        DecisionNode classifyNode,
        DecisionState state,
        DataStore data,
        IReadOnlyDictionary<string, string> templateParameters)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(classifyNode);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(templateParameters);

        var system = tree.SystemPrompt
                     ?? "Classify the user request using exactly one of the allowed answers. Return JSON with answer, observation, and confidence.";
        var classificationTask = classifyNode.Task ?? string.Empty;
        var task = tree.Task ?? string.Empty;
        foreach (var parameter in templateParameters)
        {
            var placeholder = "{{" + parameter.Key + "}}";
            task = task.Replace(placeholder, parameter.Value, StringComparison.Ordinal);
            classificationTask = classificationTask.Replace(placeholder, parameter.Value, StringComparison.Ordinal);
        }

        var answers = string.Join(", ", classifyNode.Answers ?? Array.Empty<string>());
        var stateText = state.Properties.Count == 0
            ? "none"
            : string.Join(", ", state.Properties.Select(pair => $"{pair.Key}={pair.Value ?? "null"}"));
        var dataText = data.GetAll().Count == 0
            ? "none"
            : string.Join(", ", data.GetAll().Select(item =>
                $"{item.Id} [{item.Type}] from {item.Source}: {item.Content}"));
        var user = $"Task: {(string.IsNullOrWhiteSpace(task) ? "(not supplied)" : task)}\nClassification task: {classificationTask}\nAllowed answers: {answers}\nState: {stateText}\nData: {dataText}";
        return [new LlmMessage("system", system), new LlmMessage("user", user)];
    }
}
