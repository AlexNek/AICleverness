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
    public async Task ExecuteAsync_ParsesCodeFencedJsonResponse()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("```json\n{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}\n```");
        var executor = CreateExecutor(pipeline);
        var tree = new DecisionTreeModel
        {
            TreeId = "code-fence",
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
        result.Verdict.Should().Be("yes");
        result.Classifications.Should().ContainSingle().Which.Answer.Should().Be("yes");
        pipeline.CallCount.Should().Be(1);
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

    [Theory]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Warn)]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Throttle)]
    public async Task ExecuteAsync_StopsQuestionCycleAtHardNodeVisitCap(
        AiCleverness.Models.ResourceLimitAction onExceeded)
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = CreateQuestionCycleTree(new DecisionBudget
        {
            MaxNodeVisits = 3,
            MaxLlmCalls = 10,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100,
            OnExceeded = onExceeded
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.BudgetExhausted);
        result.Usage.NodeVisits.Should().Be(3);
        result.Usage.LlmCalls.Should().Be(3);
        pipeline.CallCount.Should().Be(3);
    }

    [Theory]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Warn)]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Throttle)]
    public async Task ExecuteAsync_StopsQuestionCycleAtHardLlmCallCap(
        AiCleverness.Models.ResourceLimitAction onExceeded)
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = CreateQuestionCycleTree(new DecisionBudget
        {
            MaxNodeVisits = 10,
            MaxLlmCalls = 2,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100,
            OnExceeded = onExceeded
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.BudgetExhausted);
        result.Usage.LlmCalls.Should().Be(2);
        result.Usage.NodeVisits.Should().Be(3);
        pipeline.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForUnregisteredActionFromCustomLoader()
    {
        // Arrange
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Action,
            ActionName = "missing-action",
            Transitions =
            [
                new() { Condition = "success", NextNodeId = "done" },
                new() { Condition = "transientFailure", NextNodeId = "failed" },
                new() { Condition = "permanentFailure", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("Action 'missing-action'");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForUnregisteredPredicateFromCustomLoader()
    {
        // Arrange
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Condition,
            PredicateName = "missing-predicate",
            Transitions =
            [
                new() { Condition = "true", NextNodeId = "done" },
                new() { Condition = "false", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("Predicate 'missing-predicate'");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForMissingActionTransitionFromCustomLoader()
    {
        // Arrange
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Action,
            ActionName = "collect"
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("no transition for condition 'success'");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForMissingQuestionTransitionFromCustomLoader()
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}");
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(pipeline, loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Question,
            Question = "Is the answer yes?",
            Answers = ["yes"],
            Transitions =
            [new() { Condition = "unknown", NextNodeId = "done" }]
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("no transition for condition 'yes'");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForMissingTransitionTargetFromCustomLoader()
    {
        // Arrange
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Action,
            ActionName = "collect",
            Transitions =
            [
                new() { Condition = "success", NextNodeId = "missing-node" },
                new() { Condition = "transientFailure", NextNodeId = "failed" },
                new() { Condition = "permanentFailure", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("node 'missing-node' does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_NormalTranscriptRedactsDecisionContentAndWritesDecisionSections()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"fake-secret\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = text => text.Replace(
                    "fake-secret",
                    "[REDACTED]",
                    StringComparison.Ordinal)
            };
            var executor = CreateExecutor(pipeline, defaultOptions: options);

            // Act
            var result = await executor.ExecuteAsync(CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("### Decision action:");
            content.Should().Contain("### Selected path");
            content.Should().Contain("### Parsed decision");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
            content.Should().Contain("[REDACTED]");
            content.Should().NotContain("fake-secret");
            content.Should().NotContain("## Debug runtime");
            var parsedDecision = ParsedDecisionSection(content);
            parsedDecision.Should().Contain("**Observation:**");
            parsedDecision.Should().Contain("[REDACTED]");
            parsedDecision.Should().NotContain("fake-secret");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DebugTranscriptPreservesDecisionContent()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"debug-secret\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptDebug = true
            };
            var executor = CreateExecutor(pipeline, defaultOptions: options);

            // Act
            var result = await executor.ExecuteAsync(CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("**Debug mode:** `True`");
            content.Should().Contain("debug-secret");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
            var parsedDecision = ParsedDecisionSection(content);
            parsedDecision.Should().Contain("**Observation:**");
            parsedDecision.Should().Contain("debug-secret");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string ParsedDecisionSection(string content)
    {
        var sections = content.Split("### Parsed decision", StringSplitOptions.None);
        sections.Should().HaveCount(2);
        var section = sections[1];
        var end = section.IndexOf("## Decision result", StringComparison.Ordinal);
        end.Should().BeGreaterThan(0);
        return section[..end];
    }

    private static DecisionTreeModel CreateTree()
        => new()
        {
            TreeId = "transcript-tree",
            Version = 1,
            StartNodeId = "collect",
            Budget = new DecisionBudget
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 1,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionName = "collect",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "question" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["question"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Is the evidence supported?",
                    Answers = ["supported"],
                    Transitions =
                    [
                        new() { Condition = "supported", NextNodeId = "approved" },
                        new() { Condition = "unknown", NextNodeId = "unknown" }
                    ]
                },
                ["approved"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "approved-fake-secret"
                },
                ["unknown"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "unknown"
                },
                ["failed"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "failed"
                }
            }
        };

    private static string NewDirectory()
        => Path.Combine(
            Path.GetTempPath(),
            "AiClevernessDecisionTranscriptTests",
            Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
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

    private static DecisionTreeModel CreateValidationTree(DecisionNode startNode)
        => new()
        {
            TreeId = "custom-loader-tree",
            Version = 1,
            StartNodeId = "start",
            Budget = new()
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 2,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["start"] = startNode,
                ["done"] = Terminal("done"),
                ["failed"] = Terminal("failed")
            }
        };

    private static DecisionTreeModel CreateQuestionCycleTree(DecisionBudget budget)
        => new()
        {
            TreeId = "question-cycle",
            Version = 1,
            StartNodeId = "question",
            Budget = budget,
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["question"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Should the cycle continue?",
                    Answers = ["loop"],
                    Transitions =
                    [
                        new() { Condition = "loop", NextNodeId = "question" },
                        new() { Condition = "unknown", NextNodeId = "done" }
                    ]
                },
                ["done"] = Terminal("done")
            }
        };

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
