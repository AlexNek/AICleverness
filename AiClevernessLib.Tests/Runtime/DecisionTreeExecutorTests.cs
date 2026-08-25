using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Conversation;
using AiCleverness.Runtime.DecisionTree;
using AiClevernessLib.Tests.Testing;
using FluentAssertions;
using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DecisionTreeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RunsActionConditionAndTerminalNodes()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree();

        var result = await executor.ExecuteAsync(tree);

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("approved");
        result.Usage.NodeVisits.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ReasksOnceAndRecordsTheParsedClassification()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("not-json")
            .Enqueue("{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = new DecisionTreeModel
        {
            TreeId = "question",
            Version = 1,
            StartNodeId = "question",
            Budget = new() { MaxNodeVisits = 4, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["question"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Is it yes?",
                    Answers = ["yes"],
                    Transitions =
                    [
                        new() { Condition = "yes", NextNodeId = "done" },
                        new() { Condition = "unknown", NextNodeId = "unknown" }
                    ]
                },
                ["done"] = Terminal("yes"),
                ["unknown"] = Terminal("unknown")
            }
        };

        var result = await executor.ExecuteAsync(tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Classifications.Should().ContainSingle().Which.Answer.Should().Be("yes");
        result.Usage.LlmCalls.Should().Be(2);
        pipeline.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsUnknownAfterTheSecondInvalidAnswer()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("not-json")
            .Enqueue("{\"answer\":\"not-allowed\"}");
        var executor = CreateExecutor(pipeline);
        var tree = new DecisionTreeModel
        {
            TreeId = "unknown",
            Version = 1,
            StartNodeId = "question",
            Budget = new() { MaxNodeVisits = 3, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["question"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Is it yes?",
                    Answers = ["yes"],
                    Transitions =
                    [new() { Condition = "yes", NextNodeId = "done" }, new() { Condition = "unknown", NextNodeId = "unknown" }]
                },
                ["done"] = Terminal("yes"),
                ["unknown"] = Terminal("unknown")
            }
        };

        var result = await executor.ExecuteAsync(tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Unknown);
        result.Classifications.Should().ContainSingle().Which.Answer.Should().Be("unknown");
        result.Usage.LlmCalls.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_HaltsWhenTheNodeVisitBudgetIsReached()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree() with
        {
            Budget = new() { MaxNodeVisits = 2, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 }
        };

        var result = await executor.ExecuteAsync(tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.BudgetExhausted);
        result.Usage.NodeVisits.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_IsolatesConcurrentRuns()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => executor.ExecuteAsync(tree)));

        results.Should().OnlyContain(result => result.Succeeded);
        results.Select(result => result.ExecutionId).Distinct().Should().HaveCount(8);
        results.Should().OnlyContain(result => result.Usage.NodeVisits == 3);
    }

    private static DecisionTreeExecutor CreateExecutor(
        ILlmCompletionPipeline? pipeline = null)
        => new(
            pipeline ?? new DecisionTreeCompletionPipeline(),
            new DefaultConversationManager(),
            new InMemoryExecutionJournal(),
            null,
            [new DecisionTreeTestAction()],
            [new DataExistsPredicate()],
            new DefaultDecisionLlmContextBuilder(),
            new DecisionTreeLoader([new DecisionTreeTestAction()], [new DataExistsPredicate()]));

    private static DecisionTreeModel CreateActionConditionTree()
        => new()
        {
            TreeId = "action-condition",
            Version = 1,
            StartNodeId = "collect",
            Budget = new() { MaxNodeVisits = 10, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionName = "collect",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "check" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["check"] = new()
                {
                    Type = EDecisionNodeType.Condition,
                    PredicateName = "dataExists",
                    PredicateParameters = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["type"] = JsonDocument.Parse("\"evidence\"").RootElement.Clone()
                    },
                    Transitions =
                    [
                        new() { Condition = "true", NextNodeId = "approved" },
                        new() { Condition = "false", NextNodeId = "failed" }
                    ]
                },
                ["approved"] = Terminal("approved"),
                ["failed"] = Terminal("failed")
            }
        };

    private static DecisionNode Terminal(string verdict)
        => new() { Type = EDecisionNodeType.Terminal, Verdict = verdict };
}
