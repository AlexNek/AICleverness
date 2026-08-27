using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DefaultDecisionLlmContextBuilderTests
{
    [Fact]
    public void Build_UsesClassifyTaskLabelAndPreservesTreeTaskLabel()
    {
        var builder = new DefaultDecisionLlmContextBuilder();
        var tree = new DecisionTree
        {
            TreeId = "context-tree",
            StartNodeId = "classify",
            Task = "Review the evidence",
            SystemPrompt = "Classify the evidence."
        };
        var classifyNode = new DecisionNode
        {
            Type = EDecisionNodeType.Classify,
            Task = "Is {{subject}} supported?",
            Answers = ["supported", "unsupported"]
        };

        var messages = builder.Build(
            tree,
            classifyNode,
            new DecisionState(),
            new DataStore(),
            new Dictionary<string, string> { ["subject"] = "the evidence" });

        messages.Should().HaveCount(2);
        messages[1].Content.Should().Contain("Task: Review the evidence");
        messages[1].Content.Should().Contain("Classification task: Is the evidence supported?");
        messages[1].Content.Should().NotContain("Question:");
    }
}
