using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;
using AiCleverness.Models.DecisionTree;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiCleverness.Runtime;

/// <summary>Source-generated JSON serializer context for AOT and trimming compatibility.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(DecisionTreeModel))]
[JsonSerializable(typeof(DecisionNode))]
[JsonSerializable(typeof(DecisionTransition))]
[JsonSerializable(typeof(DecisionBudget))]
[JsonSerializable(typeof(Dictionary<string, DecisionNode>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<DecisionTransition>))]
[JsonSerializable(typeof(DecisionNodeVisitedEvent))]
[JsonSerializable(typeof(DecisionActionCompletedEvent))]
[JsonSerializable(typeof(DecisionClassificationCompletedEvent))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
internal partial class AiClevernessJsonContext : JsonSerializerContext
{
}
