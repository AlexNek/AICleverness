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
    public async Task ExecuteAsync_UsesInjectedLoaderForValidation()
    {
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);

        await executor.ExecuteAsync(CreateActionConditionTree());

        loader.ValidateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_EmitsNodeVisitWhenPredicateFails()
    {
        var predicate = new ThrowingDecisionPredicate();
        var journal = new InMemoryExecutionJournal();
        var publisher = new RecordingExecutionEventPublisher();
        var executor = CreateExecutor(
            predicates: [predicate],
            loader: new DecisionTreeLoader([], [predicate]),
            journal: journal,
            publisher: publisher);
        var tree = new DecisionTreeModel
        {
            TreeId = "predicate-failure",
            Version = 1,
            StartNodeId = "check",
            Budget = new() { MaxNodeVisits = 2, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["check"] = new()
                {
                    Type = EDecisionNodeType.Condition,
                    PredicateName = predicate.Name,
                    Transitions =
                    [
                        new() { Condition = "true", NextNodeId = "done" },
                        new() { Condition = "false", NextNodeId = "done" }
                    ]
                },
                ["done"] = Terminal("done")
            }
        };

        var result = await executor.ExecuteAsync(tree);
        var entries = await journal.ReadAfterAsync(result.ExecutionId, 0);

        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Usage.NodeVisits.Should().Be(1);
        entries.Should().ContainSingle(entry => entry.EventType == "DecisionNodeVisited");
        publisher.Events.OfType<DecisionNodeVisitedBusEvent>()
            .Should().ContainSingle(eventRecord => eventRecord.OutcomeJson == "validationFailed");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExplicitBudgetThatEqualsLibraryDefault()
    {
        var executor = CreateExecutor(
            defaultOptions: new DecisionTreeExecutionOptions
            {
                DefaultMaxNodeVisits = 2,
                DefaultMaxLlmCalls = 0,
                DefaultMaxElapsedTime = TimeSpan.FromSeconds(10),
                DefaultMaxContextTokens = 100
            });
        var tree = CreateActionConditionTree() with
        {
            Budget = new()
            {
                MaxNodeVisits = 20,
                MaxLlmCalls = 0,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            }
        };

        var result = await executor.ExecuteAsync(tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Usage.NodeVisits.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFreshCustomConversationManagerFromFactory()
    {
        var factory = new RecordingConversationManagerFactory();
        var executor = CreateExecutor(factory: factory);
        var tree = CreateActionConditionTree();

        await executor.ExecuteAsync(tree);
        await executor.ExecuteAsync(tree);

        factory.Created.Should().HaveCount(2);
        factory.Created[0].Should().NotBeSameAs(factory.Created[1]);
    }    [Fact]
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
        ILlmCompletionPipeline? pipeline = null,
        DecisionTreeExecutionOptions? defaultOptions = null,
        IDecisionTreeLoader? loader = null,
        IEnumerable<IDecisionPredicate>? predicates = null,
        IExecutionJournal? journal = null,
        IExecutionEventPublisher? publisher = null,
        IConversationManager? conversationManager = null,
        IConversationManagerFactory? factory = null)
    {
        var action = new DecisionTreeTestAction();
        var registeredPredicates = predicates?.ToArray() ?? [new DataExistsPredicate()];
        return new(
            pipeline ?? new DecisionTreeCompletionPipeline(),
            conversationManager ?? new DefaultConversationManager(),
            journal ?? new InMemoryExecutionJournal(),
            publisher,
            [action],
            registeredPredicates,
            new DefaultDecisionLlmContextBuilder(),
            loader ?? new DecisionTreeLoader([action], registeredPredicates),
            defaultOptions,
            factory);
    }

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
