using System.Net;
using System.Net.Http;
using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Conversation;
using AiCleverness.Runtime.DecisionTree;
using AiCleverness.Runtime.Transcript;
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

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("approved");
        result.Usage.NodeVisits.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ExposesActionStatePropertiesAndFiltersNullValues()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree();

        var result = await executor.ExecuteAsync(CreateActions(), 
            tree,
            new Dictionary<string, string>
            {
                ["state-property"] = "discovered-value"
            });

        result.StateProperties.Should().NotBeNull();
        result.StateProperties.Should().ContainKey("directProperty")
            .WhoseValue.Should().Be("discovered-value");
        result.StateProperties.Should().ContainKey("returnedProperty")
            .WhoseValue.Should().Be("discovered-value");
        result.StateProperties.Should().NotContainKey("nullProperty");
    }

    [Fact]
    public void DecisionTreeResult_PreservesTheLegacyConstructor()
    {
        var usage = new ResourceUsage();
        var result = new DecisionTreeResult(
            "execution-id",
            true,
            "winner",
            DecisionTreeOutcome.Terminal,
            [],
            usage,
            Error: null);

        result.StateProperties.Should().BeNull();

        var (executionId, succeeded, verdict, outcome, classifications, deconstructedUsage, error) = result;

        executionId.Should().Be("execution-id");
        succeeded.Should().BeTrue();
        verdict.Should().Be("winner");
        outcome.Should().Be(DecisionTreeOutcome.Terminal);
        classifications.Should().BeEmpty();
        deconstructedUsage.Should().BeSameAs(usage);
        error.Should().BeNull();
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
            TreeId = "classify",
            Version = 1,
            StartNodeId = "classify",
            Budget = new() { MaxNodeVisits = 4, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Is it yes?",
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

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Classifications.Should().ContainSingle().Which.Answer.Should().Be("yes");
        result.Usage.LlmCalls.Should().Be(2);
        pipeline.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_UsesNoContextOverloadAndNullModelByDefault()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);

        await executor.ExecuteAsync(CreateActions(), CreateTree());

        pipeline.NoContextCallCount.Should().Be(1);
        pipeline.ContextCallCount.Should().Be(0);
        pipeline.Requests.Should().ContainSingle().Which.Options!.Model.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_PassesConfiguredPrimaryAndFallbackContext()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var options = new DecisionTreeExecutionOptions
        {
            EnableModelFailover = true,
            Model = "primary",
            ModelFallbackChain = ["fallback"]
        };
        var executor = CreateExecutor(pipeline, defaultOptions: options);

        await executor.ExecuteAsync(CreateActions(), CreateTree());

        pipeline.NoContextCallCount.Should().Be(0);
        pipeline.ContextCallCount.Should().Be(1);
        pipeline.Requests.Should().ContainSingle().Which.Options!.Model.Should().Be("primary");
        var context = pipeline.Contexts.Should().ContainSingle().Subject;
        context.AgentContext.Should().NotBeNull();
        context.AgentContext!.AgentName.Should().Be("decision-tree");
        context.AgentContext.Goal.Should().Be("Decision tree LLM");
        context.AgentContext.State.Status.Should().Be("Running");
        context.AgentContext.GetProperty<bool>(AgentPropertyKeys.EnableModelFailover).Should().BeTrue();
        context.AgentContext.GetProperty<string>(AgentPropertyKeys.Model).Should().Be("primary");
        context.AgentContext.GetProperty<IReadOnlyList<string>>(AgentPropertyKeys.ModelFallbackChain)
            .Should().Equal("fallback");
        context.RuntimeOptions!.EnableModelFailover.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCreateContextWithoutCompleteFailoverConfiguration()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(
            pipeline,
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                Model = "primary",
                ModelFallbackChain = []
            });

        await executor.ExecuteAsync(CreateActions(), CreateTree());

        pipeline.NoContextCallCount.Should().Be(1);
        pipeline.ContextCallCount.Should().Be(0);
        pipeline.Requests.Should().ContainSingle().Which.Options!.Model.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCreateContextWhenPrimaryModelIsMissing()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(
            pipeline,
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                ModelFallbackChain = ["fallback"]
            });

        await executor.ExecuteAsync(CreateActions(), CreateTree());

        pipeline.NoContextCallCount.Should().Be(1);
        pipeline.ContextCallCount.Should().Be(0);
        pipeline.Requests.Should().ContainSingle().Which.Options!.Model.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReusesCompletionContextAcrossClassificationNodes()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(
            pipeline,
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                Model = "primary",
                ModelFallbackChain = ["fallback"]
            });

        await executor.ExecuteAsync(CreateActions(), CreateClassificationCycleTree(new DecisionBudget
        {
            MaxNodeVisits = 3,
            MaxLlmCalls = 3,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100
        }));

        pipeline.Contexts.Should().HaveCount(3);
        pipeline.Contexts.Should().OnlyContain(context => ReferenceEquals(context, pipeline.Contexts[0]));
    }

    [Fact]
    public async Task ExecuteAsync_DefaultPipelineFailsOverOnTransientProviderFailure()
    {
        var client = new DecisionTreeFailoverLlmClient(
            _ => Task.FromException<LlmResponse>(
                new HttpRequestException("HTTP 503 service unavailable", null, HttpStatusCode.ServiceUnavailable)),
            _ => Task.FromResult(new LlmResponse(
                "{\"answer\":\"supported\",\"observation\":\"fallback\",\"confidence\":\"high\"}")));
        var executor = CreateExecutor(
            new DefaultLlmCompletionPipeline(client),
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                Model = "primary",
                ModelFallbackChain = ["fallback"]
            });

        var result = await executor.ExecuteAsync(CreateActions(), CreateTree());

        result.Succeeded.Should().BeTrue();
        client.RequestedModels.Should().Equal("primary", "fallback");
    }

    [Fact]
    public async Task ExecuteAsync_PreservesFailoverStateAcrossClassificationNodes()
    {
        var client = new DecisionTreeFailoverLlmClient(
            _ => Task.FromException<LlmResponse>(
                new HttpRequestException("HTTP 503 service unavailable", null, HttpStatusCode.ServiceUnavailable)),
            _ => Task.FromResult(new LlmResponse(
                "{\"answer\":\"loop\",\"observation\":\"fallback\",\"confidence\":\"high\"}")),
            _ => Task.FromException<LlmResponse>(
                new HttpRequestException("HTTP 503 service unavailable", null, HttpStatusCode.ServiceUnavailable)),
            _ => Task.FromResult(new LlmResponse(
                "{\"answer\":\"loop\",\"observation\":\"fallback\",\"confidence\":\"high\"}")),
            _ => Task.FromException<LlmResponse>(
                new HttpRequestException("HTTP 503 service unavailable", null, HttpStatusCode.ServiceUnavailable)));
        var executor = CreateExecutor(
            new DefaultLlmCompletionPipeline(client),
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                Model = "primary",
                ModelFallbackChain = ["fallback-1", "fallback-2"]
            });

        var result = await executor.ExecuteAsync(CreateActions(), CreateClassificationChainTree(new DecisionBudget
        {
            MaxNodeVisits = 4,
            MaxLlmCalls = 5,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100
        }));

        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        client.RequestedModels.Should().Equal(
            "primary",
            "fallback-1",
            "fallback-1",
            "fallback-2",
            "fallback-2");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultPipelineDoesNotFailOverOnPermanentProviderFailure()
    {
        var client = new DecisionTreeFailoverLlmClient(
            _ => Task.FromException<LlmResponse>(new InvalidOperationException("model not found")),
            _ => Task.FromResult(new LlmResponse(
                "{\"answer\":\"supported\",\"observation\":\"fallback\",\"confidence\":\"high\"}")));
        var executor = CreateExecutor(
            new DefaultLlmCompletionPipeline(client),
            defaultOptions: new DecisionTreeExecutionOptions
            {
                EnableModelFailover = true,
                Model = "primary",
                ModelFallbackChain = ["fallback"]
            });

        var result = await executor.ExecuteAsync(CreateActions(), CreateTree());

        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        client.RequestedModels.Should().ContainSingle().Which.Should().Be("primary");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsClassificationJournalAndBusContracts()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var journal = new InMemoryExecutionJournal();
        var publisher = new RecordingExecutionEventPublisher();
        var executor = CreateExecutor(pipeline, journal: journal, publisher: publisher);

        var result = await executor.ExecuteAsync(CreateActions(), CreateTree());

        var entries = await journal.ReadAllAsync(result.ExecutionId);
        var journalEntry = entries.Should()
            .ContainSingle(entry => entry.EventType == "DecisionClassificationCompleted")
            .Which;
        journalEntry.SerializedPayload.Should().NotBeNull();
        using var payload = JsonDocument.Parse(journalEntry.SerializedPayload!);
        payload.RootElement.GetProperty("answer").GetString().Should().Be("supported");
        payload.RootElement.GetProperty("observation").GetString().Should().Be("evidence");
        payload.RootElement.GetProperty("confidence").GetString().Should().Be("high");
        payload.RootElement.GetProperty("attempt").GetInt32().Should().Be(1);

        var busEvent = publisher.Events
            .OfType<DecisionClassificationCompletedBusEvent>()
            .Should()
            .ContainSingle()
            .Which;
        busEvent.EventType.Should().Be("DecisionClassificationCompleted");
        busEvent.Answer.Should().Be("supported");
        busEvent.Observation.Should().Be("evidence");
        busEvent.Confidence.Should().Be("high");
        busEvent.Attempt.Should().Be(1);
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
            StartNodeId = "classify",
            Budget = new() { MaxNodeVisits = 3, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Is it yes?",
                    Answers = ["yes"],
                    Transitions =
                    [new() { Condition = "yes", NextNodeId = "done" }, new() { Condition = "unknown", NextNodeId = "unknown" }]
                },
                ["done"] = Terminal("yes"),
                ["unknown"] = Terminal("unknown")
            }
        };

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Unknown);
        result.Classifications.Should().ContainSingle().Which.Answer.Should().Be("unknown");
        result.Usage.LlmCalls.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsActionableUnknownWhenRequiredClassificationInputIsTruncated()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"yes\",\"observation\":\"should-not-be-called\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = CreateTree() with
        {
            Budget = new()
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 1,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            }
        };
        var parameters = new Dictionary<string, string>
        {
            ["evidence-content"] = new string('x', 2_000)
        };

        var result = await executor.ExecuteAsync(CreateActions(), tree, parameters);

        result.Outcome.Should().Be(DecisionTreeOutcome.Unknown);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("required user input was omitted");
        result.Usage.LlmCalls.Should().Be(0);
        pipeline.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesNormalSizedDecisionDataInCompletionRequest()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);

        var result = await executor.ExecuteAsync(CreateActions(), 
            CreateTree(),
            new Dictionary<string, string> { ["evidence-content"] = "normal fake evidence" });

        result.Succeeded.Should().BeTrue();
        pipeline.Requests.Should().ContainSingle();
        pipeline.Requests[0].Messages.Should().Contain(message =>
            message.Role == "user"
            && message.Content != null
            && message.Content.Contains("normal fake evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_PassesBoundedDataSnapshotToCustomContextBuilder()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"evidence\",\"confidence\":\"high\"}");
        var builder = new RecordingDecisionLlmContextBuilder();
        var options = new DecisionTreeExecutionOptions();
        options.DecisionDataPolicy.MaxItems = 1;
        options.DecisionDataPolicy.MaxContentLengthPerItem = 20;
        options.DecisionDataPolicy.MaxAggregateRepresentationLength = 100;
        var executor = CreateExecutor(
            pipeline,
            defaultOptions: options,
            contextBuilder: builder);
        var tree = CreateTree() with
        {
            Budget = new()
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 1,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 1_000
            }
        };

        var result = await executor.ExecuteAsync(CreateActions(), 
            tree,
            new Dictionary<string, string>
            {
                ["evidence-content"] = new string('x', 200)
            });

        result.Succeeded.Should().BeTrue();
        builder.Data.Should().NotBeNull();
        builder.Data!.GetAll().Should().ContainSingle(data => data.Type == "selection");
        builder.Data.GetAll().First(data => data.Type == "selection").Content.Should().Contain("truncated");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUnknownWhenAnEarlierRequiredUserMessageIsTruncated()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"supported\",\"observation\":\"should-not-be-called\",\"confidence\":\"high\"}");
        var contextBuilder = new FixedDecisionLlmContextBuilder(
        [
            new LlmMessage("system", "Classify the request."),
            new LlmMessage("user", new string('e', 1_000)),
            new LlmMessage("user", "Return exactly one allowed answer.")
        ]);
        var executor = CreateExecutor(pipeline, contextBuilder: contextBuilder);
        var tree = CreateTree() with
        {
            Budget = new()
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 1,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            }
        };

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Unknown);
        result.Error.Should().Contain("required user input was omitted");
        result.Usage.LlmCalls.Should().Be(0);
        pipeline.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_HaltsWhenTheNodeVisitBudgetIsReached()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree() with
        {
            Budget = new() { MaxNodeVisits = 2, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 }
        };

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.BudgetExhausted);
        result.Usage.NodeVisits.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedLoaderForValidation()
    {
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(loader: loader);

        await executor.ExecuteAsync(CreateActions(), CreateActionConditionTree());

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
            StartNodeId = "classify",
            Budget = new() { MaxNodeVisits = 4, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Is it yes?",
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

        var result = await executor.ExecuteAsync(CreateActions(), tree);

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
            loader: new DecisionTreeLoader([predicate]),
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
                    PredicateKey = predicate.Key,
                    Transitions =
                    [
                        new() { Condition = "true", NextNodeId = "done" },
                        new() { Condition = "false", NextNodeId = "done" }
                    ]
                },
                ["done"] = Terminal("done")
            }
        };

        var result = await executor.ExecuteAsync(CreateActions(), tree);
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

        var result = await executor.ExecuteAsync(CreateActions(), tree);

        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Usage.NodeVisits.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFreshCustomConversationManagerFromFactory()
    {
        var factory = new RecordingConversationManagerFactory();
        var executor = CreateExecutor(factory: factory);
        var tree = CreateActionConditionTree();

        await executor.ExecuteAsync(CreateActions(), tree);
        await executor.ExecuteAsync(CreateActions(), tree);

        factory.Created.Should().HaveCount(2);
        factory.Created[0].Should().NotBeSameAs(factory.Created[1]);
    }    [Fact]
    public async Task ExecuteAsync_IsolatesConcurrentRuns()
    {
        var executor = CreateExecutor();
        var tree = CreateActionConditionTree();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => executor.ExecuteAsync(CreateActions(), tree)));

        results.Should().OnlyContain(result => result.Succeeded);
        results.Select(result => result.ExecutionId).Distinct().Should().HaveCount(8);
        results.Should().OnlyContain(result => result.Usage.NodeVisits == 3);
    }

    [Theory]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Warn)]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Throttle)]
    public async Task ExecuteAsync_StopsClassificationCycleAtHardNodeVisitCap(
        AiCleverness.Models.ResourceLimitAction onExceeded)
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = CreateClassificationCycleTree(new DecisionBudget
        {
            MaxNodeVisits = 3,
            MaxLlmCalls = 10,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100,
            OnExceeded = onExceeded
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.BudgetExhausted);
        result.Usage.NodeVisits.Should().Be(3);
        result.Usage.LlmCalls.Should().Be(3);
        pipeline.CallCount.Should().Be(3);
    }

    [Theory]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Warn)]
    [InlineData(AiCleverness.Models.ResourceLimitAction.Throttle)]
    public async Task ExecuteAsync_StopsClassificationCycleAtHardLlmCallCap(
        AiCleverness.Models.ResourceLimitAction onExceeded)
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}")
            .Enqueue("{\"answer\":\"loop\",\"observation\":\"cycle\",\"confidence\":\"high\"}");
        var executor = CreateExecutor(pipeline);
        var tree = CreateClassificationCycleTree(new DecisionBudget
        {
            MaxNodeVisits = 10,
            MaxLlmCalls = 2,
            MaxElapsedTime = TimeSpan.FromSeconds(10),
            MaxContextTokens = 100,
            OnExceeded = onExceeded
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

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
            ActionKey = "missing-action",
            Transitions =
            [
                new() { Condition = "success", NextNodeId = "done" },
                new() { Condition = "transientFailure", NextNodeId = "failed" },
                new() { Condition = "permanentFailure", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

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
            PredicateKey = "missing-predicate",
            Transitions =
            [
                new() { Condition = "true", NextNodeId = "done" },
                new() { Condition = "false", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

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
            ActionKey = "collect"
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("no transition for condition 'success'");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailedForMissingClassificationTransitionFromCustomLoader()
    {
        // Arrange
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}");
        var loader = new SpyDecisionTreeLoader();
        var executor = CreateExecutor(pipeline, loader: loader);
        var tree = CreateValidationTree(new DecisionNode
        {
            Type = EDecisionNodeType.Classify,
            Task = "Is the answer yes?",
            Answers = ["yes"],
            Transitions =
            [new() { Condition = "unknown", NextNodeId = "done" }]
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

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
            ActionKey = "collect",
            Transitions =
            [
                new() { Condition = "success", NextNodeId = "missing-node" },
                new() { Condition = "transientFailure", NextNodeId = "failed" },
                new() { Condition = "permanentFailure", NextNodeId = "failed" }
            ]
        });

        // Act
        var result = await executor.ExecuteAsync(CreateActions(), tree);

        // Assert
        result.Outcome.Should().Be(DecisionTreeOutcome.ValidationFailed);
        result.Error.Should().Contain("node 'missing-node' does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_CustomTranscriptUsesReadableActionNameAndOutcomeSummary()
    {
        // Arrange
        var directory = NewDirectory();
        RecordingTranscriptSink? sink = null;
        var options = new DecisionTreeExecutionOptions
        {
            TranscriptDirectory = directory,
            TranscriptRedactor = static text => text.Replace(
                "fake-secret",
                "[REDACTED]",
                StringComparison.Ordinal),
            TranscriptBuilderFactory = static () => new MarkdownTranscriptBuilder(),
            TranscriptSinkFactory = path => sink = new RecordingTranscriptSink(path)
        };
        var executor = CreateExecutor(defaultOptions: options);
        var tree = new DecisionTreeModel
        {
            TreeId = "readable-action",
            Version = 1,
            StartNodeId = "collect",
            Budget = new()
            {
                MaxNodeVisits = 2,
                MaxLlmCalls = 0,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionKey = "collect",
                    Name = "Collect fake-secret evidence",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "completed" },
                        new() { Condition = "transientFailure", NextNodeId = "completed" },
                        new() { Condition = "permanentFailure", NextNodeId = "completed" }
                    ]
                },
                ["completed"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "completed"
                }
            }
        };
        var action = new ConfigurableTestAction(
            "collect",
            new DecisionActionResult(null, null, DecisionActionStatus.Success)
            {
                OutcomeSummary = "Found fake-secret evidence."
            });

        // Act
        var result = await executor.ExecuteAsync([action], tree);

        // Assert
        result.Succeeded.Should().BeTrue();
        sink.Should().NotBeNull();
        sink!.IsCompleted.Should().BeTrue();
        sink.Content.Should().Contain("### Decision action: `Collect [REDACTED] evidence`");
        sink.Content.Should().Contain("**Outcome:**");
        sink.Content.Should().Contain("Found [REDACTED] evidence.");
        sink.Content.Should().NotContain("fake-secret");
        sink.Content.Should().NotContain("### Decision action: `collect`");
        Directory.Exists(directory).Should().BeFalse();
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
            var result = await executor.ExecuteAsync(CreateActions(), CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("### Decision action:");
            content.Should().Contain("### Selected path");
            content.Should().Contain("### Parsed classification");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
            content.Should().Contain("[REDACTED]");
            content.Should().NotContain("fake-secret");
            content.Should().NotContain("## Debug runtime");
            var parsedDecision = ParsedClassificationSection(content);
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
    public async Task ExecuteAsync_NormalTranscriptRendersRedactedStateProperties()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"found\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = text => text
                    .Replace("directProperty", "[STATE-KEY]", StringComparison.Ordinal)
                    .Replace("state-secret", "[STATE-VALUE]", StringComparison.Ordinal)
            };
            var executor = CreateExecutor(pipeline, defaultOptions: options);

            // Act
            var result = await executor.ExecuteAsync(
                CreateActions(),
                CreateTree(),
                new Dictionary<string, string>
                {
                    ["state-property"] = "state-secret"
                });

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("### State properties");
            content.Should().Contain("**[STATE-KEY]:** `[STATE-VALUE]`");
            content.Should().Contain("**returnedProperty:** `[STATE-VALUE]`");
            content.Should().NotContain("state-secret");
            content.IndexOf("### State properties", StringComparison.Ordinal)
                .Should().BeLessThan(content.IndexOf("### Selected path", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_StatePropertiesPreserveRedactedKeyCollisionsAndInvariantValues()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"found\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = text => text
                    .Replace("collision-first-secret", "[COLLISION-KEY]", StringComparison.Ordinal)
                    .Replace("collision-second-secret", "[COLLISION-KEY]", StringComparison.Ordinal)
            };
            var executor = CreateExecutor(pipeline, defaultOptions: options);

            // Act
            var result = await executor.ExecuteAsync(
                CreateActions(),
                CreateTree(),
                new Dictionary<string, string>
                {
                    ["state-collision"] = "true",
                    ["state-non-string"] = "true"
                });

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Split("**[COLLISION-KEY]:**", StringSplitOptions.None)
                .Should().HaveCount(3);
            content.Should().Contain("**numericProperty:** `1234.5`");
            content.Should().Contain("first-value");
            content.Should().Contain("second-value");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_StatePropertyLimitsTruncateEntriesAndReportOmissions()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"found\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = static text => text
            };
            options.DecisionTranscriptPolicy.MaxStateProperties = 2;
            options.DecisionTranscriptPolicy.MaxStatePropertyKeyLength = 40;
            options.DecisionTranscriptPolicy.MaxStatePropertyValueLength = 40;
            var executor = CreateExecutor(pipeline, defaultOptions: options);
            var tree = CreateTree() with
            {
                Budget = new()
                {
                    MaxNodeVisits = 5,
                    MaxLlmCalls = 1,
                    MaxElapsedTime = TimeSpan.FromSeconds(10),
                    MaxContextTokens = 1_000
                }
            };

            // Act
            var result = await executor.ExecuteAsync(
                CreateActions(),
                tree,
                new Dictionary<string, string>
                {
                    ["state-property"] = new string('x', 80),
                    ["state-long-key"] = "short"
                });

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("[state property value truncated]");
            content.Should().Contain("[state property key truncated]");
            content.Should().Contain("**[state properties omitted]:** `1`");
            var decisionResult = content[content.IndexOf("## Decision result", StringComparison.Ordinal)..];
            decisionResult.Should().NotContain(new string('x', 80));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DebugTranscriptPreservesStatePropertyContent()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"found\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptDebug = true
            };
            var executor = CreateExecutor(pipeline, defaultOptions: options);

            // Act
            var result = await executor.ExecuteAsync(
                CreateActions(),
                CreateTree(),
                new Dictionary<string, string>
                {
                    ["state-property"] = "debug-state-secret"
                });

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("### State properties");
            content.Should().Contain("debug-state-secret");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_BoundsDecisionTranscriptContentAfterRedaction()
    {
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"response-secret-very-long\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = text => text.Replace("secret", "[REDACTED]", StringComparison.Ordinal)
            };
            options.DecisionDataPolicy.MaxContentLengthPerItem = 20;
            options.DecisionDataPolicy.MaxAggregateRepresentationLength = 100;
            options.DecisionTranscriptPolicy.MaxContentLength = 30;
            options.DecisionTranscriptPolicy.MaxMessageContentLength = 40;
            options.DecisionTranscriptPolicy.MaxResponseContentLength = 40;
            options.DecisionTranscriptPolicy.MaxTotalCharacters = 5_000;
            var executor = CreateExecutor(pipeline, defaultOptions: options);
            var tree = CreateTree() with
            {
                Budget = new()
                {
                    MaxNodeVisits = 5,
                    MaxLlmCalls = 1,
                    MaxElapsedTime = TimeSpan.FromSeconds(10),
                    MaxContextTokens = 1_000
                }
            };

            // Act
            var result = await executor.ExecuteAsync(CreateActions(), 
                tree,
                new Dictionary<string, string>
                {
                    ["evidence-content"] = "evidence-secret-very-long"
                });

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("[REDACTED]");
            content.Should().Contain("truncated");
            content.Should().NotContain("evidence-secret-very-long");
            content.Should().NotContain("response-secret-very-long");
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
            var result = await executor.ExecuteAsync(CreateActions(), CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("**Debug mode:** `True`");
            content.Should().Contain("debug-secret");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
            var parsedDecision = ParsedClassificationSection(content);
            parsedDecision.Should().Contain("**Observation:**");
            parsedDecision.Should().Contain("debug-secret");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string ParsedClassificationSection(string content)
    {
        var sections = content.Split("### Parsed classification", StringSplitOptions.None);
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
                    ActionKey = "collect",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "classify" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Is the evidence supported?",
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

    [Fact]
    public async Task ExecuteAsync_ActionFailsButFallbackReachesTerminal_ReportsTerminalOutcome()
    {
        var primary = new ConfigurableTestAction(
            "primary",
            ActionResult(DecisionActionStatus.PermanentFailure, "primary action failed"));
        var fallback = new ConfigurableTestAction("fallback", SuccessfulActionResult());
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync([primary, fallback], CreateActionFallbackTree());

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("recovered");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ActionFailsWithTransientThenPermanent_FallbackReachesTerminal()
    {
        var primary = new ConfigurableTestAction(
            "primary",
            ActionResult(DecisionActionStatus.TransientFailure, "temporary action failure"),
            ActionResult(DecisionActionStatus.PermanentFailure, "permanent action failure"));
        var fallback = new ConfigurableTestAction("fallback", SuccessfulActionResult());
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync([primary, fallback], CreateActionFallbackTree(retryTransientFailure: true));

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("recovered");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_AllActionsSucceed_ReportsTerminalOutcome()
    {
        var action = new ConfigurableTestAction("action", SuccessfulActionResult());
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync([action], CreateActionSuccessTree());

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("completed");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ActionFailsWithDirectTerminalFallback_ReportsTerminalOutcome()
    {
        var action = new ConfigurableTestAction(
            "action",
            ActionResult(DecisionActionStatus.PermanentFailure, "action failed"));
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync([action], CreateDirectTerminalFallbackTree());

        result.Succeeded.Should().BeTrue();
        result.Outcome.Should().Be(DecisionTreeOutcome.Terminal);
        result.Verdict.Should().Be("skip");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ActionFailureThenUnknownClassification_PreservesClassificationError()
    {
        var pipeline = new DecisionTreeCompletionPipeline()
            .Enqueue("not-json")
            .Enqueue("{\"answer\":\"not-allowed\"}");
        var action = new ConfigurableTestAction(
            "primary",
            ActionResult(DecisionActionStatus.PermanentFailure, "primary action failed"));
        var executor = CreateExecutor(pipeline: pipeline);

        var result = await executor.ExecuteAsync([action], CreateActionUnknownTree());

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(DecisionTreeOutcome.Unknown);
        result.Error.Should().Contain("Classification response could not be classified.");
        result.Error.Should().NotContain("primary action failed");
    }

    private static DecisionActionResult SuccessfulActionResult()
        => ActionResult(DecisionActionStatus.Success);

    private static DecisionActionResult ActionResult(DecisionActionStatus status, string? error = null)
        => new(null, null, status, error);

    private static DecisionTreeModel CreateActionFallbackTree(bool retryTransientFailure = false)
    {
        var transientTarget = retryTransientFailure ? "primary" : "fallback";
        return new()
        {
            TreeId = "action-fallback",
            Version = 1,
            StartNodeId = "primary",
            Budget = new() { MaxNodeVisits = 10, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["primary"] = ActionNode("primary", "fallback", transientTarget, "fallback"),
                ["fallback"] = ActionNode("fallback", "recovered", "recovered", "recovered"),
                ["recovered"] = Terminal("recovered")
            }
        };
    }

    private static DecisionTreeModel CreateActionSuccessTree()
        => new()
        {
            TreeId = "action-success",
            Version = 1,
            StartNodeId = "action",
            Budget = new() { MaxNodeVisits = 3, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["action"] = ActionNode("action", "completed", "completed", "completed"),
                ["completed"] = Terminal("completed")
            }
        };

    private static DecisionTreeModel CreateDirectTerminalFallbackTree()
        => new()
        {
            TreeId = "direct-terminal-fallback",
            Version = 1,
            StartNodeId = "action",
            Budget = new() { MaxNodeVisits = 3, MaxLlmCalls = 0, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["action"] = ActionNode("action", "skip", "skip", "skip"),
                ["skip"] = Terminal("skip")
            }
        };

    private static DecisionTreeModel CreateActionUnknownTree()
        => new()
        {
            TreeId = "action-unknown",
            Version = 1,
            StartNodeId = "primary",
            Budget = new() { MaxNodeVisits = 6, MaxLlmCalls = 2, MaxElapsedTime = TimeSpan.FromSeconds(10), MaxContextTokens = 100 },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["primary"] = ActionNode("primary", "classify", "classify", "classify"),
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Is the answer yes?",
                    Answers = ["yes"],
                    Transitions =
                    [
                        new() { Condition = "yes", NextNodeId = "completed" },
                        new() { Condition = "unknown", NextNodeId = "unknown" }
                    ]
                },
                ["completed"] = Terminal("yes"),
                ["unknown"] = Terminal("unknown")
            }
        };

    private static DecisionNode ActionNode(
        string actionKey,
        string successTarget,
        string transientFailureTarget,
        string permanentFailureTarget)
        => new()
        {
            Type = EDecisionNodeType.Action,
            ActionKey = actionKey,
            Transitions =
            [
                new() { Condition = "success", NextNodeId = successTarget },
                new() { Condition = "transientFailure", NextNodeId = transientFailureTarget },
                new() { Condition = "permanentFailure", NextNodeId = permanentFailureTarget }
            ]
        };

    private static IReadOnlyList<IDecisionAction> CreateActions()
        => [new DecisionTreeTestAction()];

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
        IConversationManagerFactory? factory = null,
        IDecisionLlmContextBuilder? contextBuilder = null,
        IDecisionDataPolicy? decisionDataPolicy = null)
    {
        var registeredPredicates = predicates?.ToArray() ?? [new DataExistsPredicate()];
        return new(
            pipeline ?? new DecisionTreeCompletionPipeline(),
            conversationManager ?? new DefaultConversationManager(),
            journal ?? new InMemoryExecutionJournal(),
            publisher,
            registeredPredicates,
            contextBuilder ?? new DefaultDecisionLlmContextBuilder(),
            loader ?? new DecisionTreeLoader(registeredPredicates),
            defaultOptions,
            factory,
            decisionDataPolicy);
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

    private static DecisionTreeModel CreateClassificationChainTree(DecisionBudget budget)
        => new()
        {
            TreeId = "classification-chain",
            Version = 1,
            StartNodeId = "classify-1",
            Budget = budget,
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["classify-1"] = ClassificationNode("classify-2"),
                ["classify-2"] = ClassificationNode("classify-3"),
                ["classify-3"] = ClassificationNode("done"),
                ["done"] = Terminal("done")
            }
        };

    private static DecisionNode ClassificationNode(string nextNodeId)
        => new()
        {
            Type = EDecisionNodeType.Classify,
            Task = "Should this classification continue?",
            Answers = ["loop"],
            Transitions =
            [
                new() { Condition = "loop", NextNodeId = nextNodeId },
                new() { Condition = "unknown", NextNodeId = "done" }
            ]
        };

    private static DecisionTreeModel CreateClassificationCycleTree(DecisionBudget budget)
        => new()
        {
            TreeId = "classification-cycle",
            Version = 1,
            StartNodeId = "classify",
            Budget = budget,
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["classify"] = new()
                {
                    Type = EDecisionNodeType.Classify,
                    Task = "Should the cycle continue?",
                    Answers = ["loop"],
                    Transitions =
                    [
                        new() { Condition = "loop", NextNodeId = "classify" },
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
                    ActionKey = "collect",
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
                    PredicateKey = "dataExists",
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
