using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Source-generated JSON loader and validator for decision trees.</summary>
public sealed class DecisionTreeLoader : IDecisionTreeLoader
{
    private readonly IReadOnlyDictionary<string, IDecisionAction> _actions;
    private readonly IReadOnlyDictionary<string, IDecisionPredicate> _predicates;

    public DecisionTreeLoader(
        IEnumerable<IDecisionAction>? actions = null,
        IEnumerable<IDecisionPredicate>? predicates = null)
    {
        _actions = BuildCatalog(actions);
        _predicates = BuildCatalog(predicates);
    }

    public DecisionTreeModel Load(string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        cancellationToken.ThrowIfCancellationRequested();
        var tree = JsonSerializer.Deserialize(json, AiClevernessJsonContext.Default.DecisionTree)
                   ?? throw new InvalidOperationException("Decision tree JSON did not contain a tree.");
        cancellationToken.ThrowIfCancellationRequested();
        Validate(tree);
        return tree;
    }

    public DecisionTreeModel Validate(DecisionTreeModel tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (string.IsNullOrWhiteSpace(tree.TreeId))
            throw new InvalidOperationException("Decision tree must have a non-empty treeId.");
        if (tree.Version <= 0)
            throw new InvalidOperationException("Decision tree version must be greater than zero.");
        if (string.IsNullOrWhiteSpace(tree.StartNodeId))
            throw new InvalidOperationException("Decision tree must have a non-empty startNodeId.");
        if (tree.Name is not null && string.IsNullOrWhiteSpace(tree.Name))
            throw new InvalidOperationException("Decision tree name must not be whitespace-only.");
        if (tree.Description is not null && string.IsNullOrWhiteSpace(tree.Description))
            throw new InvalidOperationException("Decision tree description must not be whitespace-only.");
        if (tree.Budget is null
            || tree.Budget.MaxNodeVisits <= 0 || tree.Budget.MaxLlmCalls < 0
            || tree.Budget.MaxElapsedTime <= TimeSpan.Zero || tree.Budget.MaxContextTokens <= 0)
            throw new InvalidOperationException("Decision tree budget values must be positive, except that maxLlmCalls may be zero.");
        if (tree.Nodes is null || tree.Nodes.Count == 0)
            throw new InvalidOperationException("Decision tree must contain at least one node.");

        foreach (var pair in tree.Nodes)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new InvalidOperationException("Decision tree node IDs must be non-empty.");
            if (pair.Value is null)
                throw new InvalidOperationException($"Decision tree node '{pair.Key}' is null.");
        }
        if (!tree.Nodes.ContainsKey(tree.StartNodeId))
            throw new InvalidOperationException($"Start node '{tree.StartNodeId}' does not exist.");

        foreach (var pair in tree.Nodes)
            ValidateNode(pair.Key, pair.Value, tree.Nodes);

        var reachable = GetReachableNodes(tree);
        if (reachable.Count != tree.Nodes.Count)
        {
            var unreachable = tree.Nodes.Keys.First(id => !reachable.Contains(id));
            throw new InvalidOperationException($"Decision tree contains unreachable node '{unreachable}'.");
        }

        foreach (var nodeId in reachable)
        {
            if (!CanReachTerminal(nodeId, tree.Nodes, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal)))
                throw new InvalidOperationException($"Node '{nodeId}' cannot reach a terminal node.");
        }

        return tree;
    }

    private void ValidateNode(
        string nodeId,
        DecisionNode node,
        IReadOnlyDictionary<string, DecisionNode> nodes)
    {
        var transitions = node.Transitions ?? Array.Empty<DecisionTransition>();
        if (transitions.Any(transition => transition is null
                                         || string.IsNullOrWhiteSpace(transition.Condition)
                                         || string.IsNullOrWhiteSpace(transition.NextNodeId)))
            throw new InvalidOperationException($"Node '{nodeId}' contains an invalid transition.");
        if (transitions.Select(transition => transition.Condition).Distinct(StringComparer.Ordinal).Count() != transitions.Count)
            throw new InvalidOperationException($"Node '{nodeId}' contains duplicate transition conditions.");
        foreach (var transition in transitions)
        {
            if (!nodes.ContainsKey(transition.NextNodeId))
                throw new InvalidOperationException($"Node '{nodeId}' targets missing node '{transition.NextNodeId}'.");
        }

        switch (node.Type)
        {
            case EDecisionNodeType.Action:
                RequireOnly(nodeId, node.Task, node.Answers, node.PredicateKey, node.PredicateParameters, node.Verdict, "action");
                RequireText(node.ActionKey, $"Action node '{nodeId}' must specify actionKey.");
                ValidateDisplayMetadata(nodeId, node);
                RequireConditions(nodeId, transitions, ["success", "transientFailure", "permanentFailure"]);
                if (!_actions.ContainsKey(node.ActionKey!))
                    throw new InvalidOperationException($"Action '{node.ActionKey}' is not registered.");
                break;
            case EDecisionNodeType.Classify:
                RequireOnly(nodeId, node.ActionKey, null, node.PredicateKey, node.PredicateParameters, node.Verdict, "classify");
                RequireText(node.Task, $"Classify node '{nodeId}' must specify task.");
                ValidateDisplayMetadata(nodeId, node);
                if (node.Answers is null || node.Answers.Count == 0 || node.Answers.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException($"Classify node '{nodeId}' must specify non-empty answers.");
                if (node.Answers.Distinct(StringComparer.Ordinal).Count() != node.Answers.Count)
                    throw new InvalidOperationException($"Classify node '{nodeId}' contains duplicate answers.");
                if (node.Answers.Contains("unknown", StringComparer.Ordinal))
                    throw new InvalidOperationException($"Classify node '{nodeId}' cannot declare reserved answer 'unknown'.");
                RequireConditions(nodeId, transitions, node.Answers.Append("unknown"));
                break;
            case EDecisionNodeType.Condition:
                RequireOnly(nodeId, node.ActionKey, node.Answers, node.Task, null, node.Verdict, "condition");
                RequireText(node.PredicateKey, $"Condition node '{nodeId}' must specify predicateKey.");
                ValidateDisplayMetadata(nodeId, node);
                RequireConditions(nodeId, transitions, ["true", "false"]);
                if (!_predicates.ContainsKey(node.PredicateKey!))
                    throw new InvalidOperationException($"Predicate '{node.PredicateKey}' is not registered.");
                break;
            case EDecisionNodeType.Terminal:
                RequireOnly(nodeId, node.ActionKey, node.Answers, node.Task, node.PredicateKey, node.PredicateParameters, "terminal");
                RequireText(node.Verdict, $"Terminal node '{nodeId}' must specify verdict.");
                ValidateDisplayMetadata(nodeId, node);
                if (transitions.Count != 0)
                    throw new InvalidOperationException($"Terminal node '{nodeId}' cannot have transitions.");
                break;
            default:
                throw new InvalidOperationException($"Node '{nodeId}' has an unsupported node type.");
        }
    }

    private static void RequireOnly(
        string nodeId,
        object? first,
        object? second,
        object? third,
        object? fourth,
        object? fifth,
        string kind)
    {
        if (new[] { first, second, third, fourth, fifth }.Any(value => value is not null))
            throw new InvalidOperationException($"Node '{nodeId}' contains fields not valid for a {kind} node.");
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(message);
    }

    private static void ValidateDisplayMetadata(string nodeId, DecisionNode node)
    {
        if (node.Name is not null && string.IsNullOrWhiteSpace(node.Name))
            throw new InvalidOperationException($"Node '{nodeId}' name must not be whitespace-only.");
        if (node.Description is not null && string.IsNullOrWhiteSpace(node.Description))
            throw new InvalidOperationException($"Node '{nodeId}' description must not be whitespace-only.");
    }

    private static void RequireConditions(
        string nodeId,
        IReadOnlyList<DecisionTransition> transitions,
        IEnumerable<string> required)
    {
        var actual = transitions.Select(transition => transition.Condition).ToHashSet(StringComparer.Ordinal);
        var expected = required.ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
            throw new InvalidOperationException($"Node '{nodeId}' must define exactly these transitions: {string.Join(", ", expected)}.");
    }

    private static HashSet<string> GetReachableNodes(DecisionTreeModel tree)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(tree.StartNodeId);
        while (pending.Count > 0)
        {
            var nodeId = pending.Pop();
            if (!result.Add(nodeId))
                continue;
            foreach (var transition in tree.Nodes[nodeId].Transitions ?? Array.Empty<DecisionTransition>())
                pending.Push(transition.NextNodeId);
        }
        return result;
    }

    private static bool CanReachTerminal(
        string nodeId,
        IReadOnlyDictionary<string, DecisionNode> nodes,
        HashSet<string> visiting,
        HashSet<string> successful)
    {
        if (successful.Contains(nodeId))
            return true;
        if (!visiting.Add(nodeId))
            return false;
        var node = nodes[nodeId];
        var reaches = node.Type == EDecisionNodeType.Terminal
                      || (node.Transitions ?? Array.Empty<DecisionTransition>()).Any(transition =>
                          CanReachTerminal(transition.NextNodeId, nodes, visiting, successful));
        visiting.Remove(nodeId);
        if (reaches)
            successful.Add(nodeId);
        return reaches;
    }

    private static IReadOnlyDictionary<string, T> BuildCatalog<T>(IEnumerable<T>? items)
        where T : class
    {
        var catalog = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items ?? Array.Empty<T>())
        {
            var name = item switch
            {
                IDecisionAction action => action.Key,
                IDecisionPredicate predicate => predicate.Key,
                _ => throw new InvalidOperationException("Unsupported decision catalog item.")
            };
            if (string.IsNullOrWhiteSpace(name) || !catalog.TryAdd(name, item))
                throw new InvalidOperationException($"Decision catalog contains duplicate or empty key '{name}'.");
        }
        return catalog;
    }
}
