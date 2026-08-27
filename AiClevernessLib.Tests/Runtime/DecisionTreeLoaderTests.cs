using System.Text.Json;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DecisionTreeLoaderTests
{
    [Fact]
    public void Load_UsesTheDocumentedCamelCaseShape()
    {
        var loader = new DecisionTreeLoader();
        const string json = """
        {
          "treeId": "simple",
          "version": 1,
          "startNodeId": "start",
          "budget": { "maxNodeVisits": 4, "maxLlmCalls": 0, "maxElapsedTime": "00:00:10", "maxContextTokens": 100 },
          "nodes": {
            "start": {
              "type": "terminal",
              "verdict": "done",
              "transitions": []
            }
          }
        }
        """;

        var tree = loader.Load(json);

        tree.TreeId.Should().Be("simple");
        tree.Nodes["start"].Type.Should().Be(EDecisionNodeType.Terminal);
    }

    [Fact]
    public void Load_RejectsUnreachableNodes()
    {
        var loader = new DecisionTreeLoader();
        var tree = new DecisionTree
        {
            TreeId = "invalid",
            Version = 1,
            StartNodeId = "start",
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["start"] = Terminal("done"),
                ["unreachable"] = Terminal("never")
            }
        };

        var act = () => loader.Validate(tree);

        act.Should().Throw<InvalidOperationException>().WithMessage("*unreachable*");
    }

    [Fact]
    public void Load_RejectsAReachableCycleWithoutTerminalPath()
    {
        var loader = new DecisionTreeLoader();
        var tree = new DecisionTree
        {
            TreeId = "cycle",
            Version = 1,
            StartNodeId = "a",
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["a"] = Classify("a", "b"),
                ["b"] = Classify("b", "a")
            }
        };

        var act = () => loader.Validate(tree);

        act.Should().Throw<InvalidOperationException>().WithMessage("*terminal*");
    }

    private static DecisionNode Terminal(string verdict)
        => new() { Type = EDecisionNodeType.Terminal, Verdict = verdict };

    private static DecisionNode Classify(string answer, string next)
        => new()
        {
            Type = EDecisionNodeType.Classify,
            Task = answer,
            Answers = ["yes"],
            Transitions =
            [
                new() { Condition = "yes", NextNodeId = next },
                new() { Condition = "unknown", NextNodeId = next }
            ]
        };
}
