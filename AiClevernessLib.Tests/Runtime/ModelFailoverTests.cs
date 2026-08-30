using System.Net;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Capabilities;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class ModelFailoverTests
{
    [Fact]
    public async Task Failover_OnTimeout_ContinuesWithNextCandidate()
    {
        // Arrange: first call times out, second succeeds.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test failover",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("response from model-b");
        llm.CallLog.Should().HaveCount(2);
        llm.CallLog[0].Model.Should().Be("model-a");
        llm.CallLog[1].Model.Should().Be("model-b");
    }

    [Fact]
    public async Task Failover_ChainExhausted_ReturnsFailureWithLastModel()
    {
        // Arrange: all models time out.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a", "model-b", "model-c"],
            succeedOnModels: []);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test exhaustion",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b", "model-c" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(EFailureKind.FailoverExhausted);
    }

    [Fact]
    public async Task Failover_DisabledByDefault_TimeoutFailsImmediately()
    {
        // Arrange: failover NOT enabled (default).
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1
                // EnableModelFailover defaults to false
            });

        var request = new AgentRequest(
            "test disabled",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" }
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — should fail immediately, not failover.
        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(EFailureKind.LlmTimeout);
        llm.CallLog.Should().HaveCount(1);
    }

    [Fact]
    public async Task Failover_PinnedModel_NeverFailsOver()
    {
        // Arrange: model is pinned (explicit Model, no chain property).
        var llm = new FailoverLlmClient(
            failOnModels: ["pinned-model"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        // Pinned: explicit model, no fallback chain.
        var request = new AgentRequest(
            "test pinned",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "pinned-model"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — should fail, not failover.
        result.Success.Should().BeFalse();
        llm.CallLog.Should().HaveCount(1);
    }

    [Fact]
    public async Task Failover_EmitsModelSwitchedEvent_InStreaming()
    {
        // Arrange
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test events",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var events = new List<AgentEvent>();
        await foreach (var evt in runtime.RunStreamingAsync(request))
        {
            events.Add(evt);
        }

        // Assert
        var switchEvent = events.OfType<ModelSwitchedAgentEvent>().SingleOrDefault();
        switchEvent.Should().NotBeNull();
        switchEvent!.From.Should().Be("model-a");
        switchEvent.To.Should().Be("model-b");

        // Event order: the transient failure precedes the switch notification,
        // and the retried turn does not emit a second turn-start event.
        var failureIndex = events.FindIndex(
            e => e is FailureEvent { IsTransient: true });
        var switchIndex = events.IndexOf(switchEvent);
        failureIndex.Should().BeGreaterThanOrEqualTo(0);
        failureIndex.Should().BeLessThan(switchIndex);
        events.OfType<TurnStartedEvent>().Should().ContainSingle()
            .Which.Turn.Should().Be(0);
    }

    [Fact]
    public async Task Failover_FailedAttempt_DoesNotConsumeTurn()
    {
        // Arrange — two LLM attempts (timeout + success) form ONE logical turn.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var stateCapture = new StateCapturingMiddleware();
        var runtime = new AgentRuntime(
            llm,
            tools,
            middleware: [stateCapture],
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test turn state",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the state turn counter stays in sync with the local turn
        // counter: the failed attempt must not consume a turn.
        result.Success.Should().BeTrue();
        llm.CallLog.Should().HaveCount(2);
        stateCapture.FinalTurnCount.Should().Be(1);
    }

    [Fact]
    public async Task Failover_ObserverCalled_OnLlmCallCompleted()
    {
        // Arrange
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var observer = new FailoverSpyObserver();
        var runtime = new AgentRuntime(
            llm,
            tools,
            observers: [observer],
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test observer",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        await runtime.RunAsync(request);

        // Assert — OnLlmCallCompletedAsync fires for each attempt.
        observer.LlmCallCompletedInfos.Should().HaveCount(2);
        observer.LlmCallCompletedInfos[0].Success.Should().BeFalse();
        observer.LlmCallCompletedInfos[0].Model.Should().Be("model-a");
        observer.LlmCallCompletedInfos[0].Classification.Should().Be(EFailureClassification.TransientAdvance);
        observer.LlmCallCompletedInfos[1].Success.Should().BeTrue();
        observer.LlmCallCompletedInfos[1].Model.Should().Be("model-b");

        // OnModelSwitchedAsync fires once.
        observer.ModelSwitches.Should().HaveCount(1);
        observer.ModelSwitches[0].From.Should().Be("model-a");
        observer.ModelSwitches[0].To.Should().Be("model-b");
    }

    [Fact]
    public async Task Failover_ProviderCapacityFailure_AdvancesAndPreservesMetadata()
    {
        // Arrange
        var llm = new ScriptedFailoverLlmClient(
            (_, _, _) => Task.FromException<LlmResponse>(
                new LlmProviderException(
                    new InvalidOperationException("provider overloaded"),
                    provider: "test-provider",
                    errorCode: "capacity-code",
                    statusCode: (HttpStatusCode)529,
                    retryAfter: TimeSpan.FromSeconds(3),
                    isTransient: true)),
            (_, _, _) => Task.FromResult(new LlmResponse("fallback response")));
        var tools = new ToolRegistry();
        var observer = new FailoverSpyObserver();
        var runtime = new AgentRuntime(
            llm,
            tools,
            observers: [observer],
            options: new AgentRuntimeOptions { EnableModelFailover = true });

        var request = new AgentRequest(
            "test provider capacity",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("fallback response");
        llm.CallLog.Select(call => call.Model).Should().Equal("model-a", "model-b");
        observer.LlmCallCompletedInfos.Should().HaveCount(2);
        observer.LlmCallCompletedInfos[0].Classification
            .Should().Be(EFailureClassification.TransientAdvance);
        observer.LlmCallCompletedInfos[0].ProviderFailure.Should().BeEquivalentTo(
            new LlmProviderFailureMetadata
            {
                Provider = "test-provider",
                ErrorCode = "capacity-code",
                StatusCode = (HttpStatusCode)529,
                RetryAfter = TimeSpan.FromSeconds(3)
            });
        observer.LlmCallCompletedInfos[1].ProviderFailure.Should().BeNull();
    }

    [Fact]
    public async Task Failover_ProviderCapacityFailure_StreamingEventPreservesMetadata()
    {
        // Arrange
        var expectedMetadata = new LlmProviderFailureMetadata
        {
            Provider = "test-provider",
            ErrorCode = "capacity-code",
            StatusCode = (HttpStatusCode)529,
            RetryAfter = TimeSpan.FromSeconds(3)
        };
        var llm = new ScriptedFailoverLlmClient(
            (_, _, _) => Task.FromException<LlmResponse>(
                new LlmProviderException(
                    new InvalidOperationException("provider overloaded"),
                    provider: expectedMetadata.Provider,
                    errorCode: expectedMetadata.ErrorCode,
                    statusCode: expectedMetadata.StatusCode,
                    retryAfter: expectedMetadata.RetryAfter,
                    isTransient: true)),
            (_, _, _) => Task.FromResult(new LlmResponse("fallback response")));
        var runtime = new AgentRuntime(
            llm,
            new ToolRegistry(),
            options: new AgentRuntimeOptions { EnableModelFailover = true });
        var request = new AgentRequest(
            "test streaming provider capacity",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var events = new List<AgentEvent>();
        await foreach (var evt in runtime.RunStreamingAsync(request))
        {
            events.Add(evt);
        }

        // Assert
        var failure = events.OfType<FailureEvent>().Single();
        failure.IsTransient.Should().BeTrue();
        failure.Phase.Should().Be("LlmCompletion");
        failure.ProviderFailure.Should().BeEquivalentTo(expectedMetadata);
        var switchEvent = events.OfType<ModelSwitchedAgentEvent>().Single();
        events.IndexOf(failure).Should().BeLessThan(events.IndexOf(switchEvent));
        events.OfType<RunCompletedEvent>().Single().Result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Failover_TurnCounterNotInflated()
    {
        // Arrange: max 2 turns. First attempt times out, second succeeds.
        // The timeout should not consume a turn.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                DefaultMaxTurns = 1, // only 1 turn allowed
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test turns",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — should succeed even with maxTurns=1
        // because the failed attempt was rewound.
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void DefaultLlmErrorClassifier_Timeout_ReturnsTransientAdvance()
    {
        var classifier = new DefaultLlmErrorClassifier();
        using var callerCts = new CancellationTokenSource();
        // Caller token NOT cancelled — simulates per-turn timeout.
        var ex = new OperationCanceledException();

        var result = classifier.Classify(ex, callerCts.Token);

        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void DefaultLlmErrorClassifier_CallerCancellation_ReturnsPermanent()
    {
        var classifier = new DefaultLlmErrorClassifier();
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        var ex = new OperationCanceledException();

        var result = classifier.Classify(ex, callerCts.Token);

        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void DefaultLlmErrorClassifier_OtherException_ReturnsPermanent()
    {
        var classifier = new DefaultLlmErrorClassifier();
        using var callerCts = new CancellationTokenSource();
        var ex = new InvalidOperationException("something broke");

        var result = classifier.Classify(ex, callerCts.Token);

        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public async Task Failover_ResolutionBasedChain_SwitchesWithoutExplicitChain()
    {
        // Arrange: capability resolution picks model-a with model-b as the
        // fallback. No explicit chain — failover must use the resolved chain.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var catalog = CreateTextCatalog();
        var manager = new DefaultModelManager(
            new DefaultCapabilityResolver([TextProfile()]),
            catalog,
            new DefaultSelectionPolicy());
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            },
            modelManager: manager,
            modelCatalog: catalog);

        var request = new AgentRequest(
            "test resolution failover",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.EnableModelFailover] = true
            },
            CapabilityRequirements: new CapabilityRequirements());

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        llm.CallLog.Should().HaveCount(2);
        llm.CallLog[0].Model.Should().Be("model-a");
        llm.CallLog[1].Model.Should().Be("model-b");
    }

    [Fact]
    public async Task Failover_ExplicitChain_UnknownNameSkipped_UsesKnownName()
    {
        // Arrange: explicit chain contains a name that is not in the catalog.
        // It must be skipped with a warning; the known name is still used.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var catalog = CreateTextCatalog();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            },
            modelCatalog: catalog);

        var request = new AgentRequest(
            "test unknown fallback name",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] =
                    new List<string> { "nonexistent-model", "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        llm.CallLog.Should().HaveCount(2);
        llm.CallLog[1].Model.Should().Be("model-b");
    }

    [Fact]
    public async Task Failover_ExplicitChain_ActiveModelAndDuplicatesSkipped()
    {
        // Arrange: the chain names the active model (twice, mixed case) and a
        // duplicate fallback — only one distinct fallback may remain.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var observer = new FailoverSpyObserver();
        var runtime = new AgentRuntime(
            llm,
            tools,
            observers: [observer],
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            },
            modelCatalog: CreateTextCatalog());

        var request = new AgentRequest(
            "test chain normalization",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] =
                    new List<string> { "model-a", "model-b", "model-b", "MODEL-A" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — one switch straight to the distinct fallback; the active
        // model is never retried via the chain.
        result.Success.Should().BeTrue();
        llm.CallLog.Select(c => c.Model).Should().Equal("model-a", "model-b");
        observer.ModelSwitches.Should().ContainSingle();
        observer.ModelSwitches[0].From.Should().Be("model-a");
        observer.ModelSwitches[0].To.Should().Be("model-b");
    }

    [Fact]
    public async Task Failover_PreservesToolResults_AcrossSwitch()
    {
        // Arrange: model-a calls a tool, then times out on the next turn.
        // The switch to model-b must carry the tool result in the conversation.
        const string toolOutput = "tool-output-42";
        var llm = new ScriptedFailoverLlmClient(
            (messages, options, ct) => Task.FromResult(new LlmResponse(
                null,
                ToolCalls: [new LlmToolCall("call-1", "echo", "{}")])),
            async (messages, options, ct) =>
            {
                // Simulate per-turn timeout (delay until CTS fires).
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new LlmResponse("never"); // unreachable
            },
            (messages, options, ct) => Task.FromResult(new LlmResponse("synthesized")));

        var tools = new ToolRegistry();
        tools.Register(new EchoTool(toolOutput));
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test conversation continuity",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — run succeeded on model-b without re-invoking the tool.
        result.Success.Should().BeTrue();
        result.Output.Should().Be("synthesized");
        llm.CallLog.Should().HaveCount(3);
        llm.CallLog[2].Model.Should().Be("model-b");

        // model-b received the tool result produced during model-a's turns.
        llm.CallLog[2].Messages.Should().Contain(m =>
            m.Role == "tool"
            && m.Content == toolOutput
            && m.ToolCallId == "call-1");
    }

    [Fact]
    public async Task Failover_ProvenanceUpdated_VisibleToObservers()
    {
        // Arrange: explicit chain with provenance but no capability resolution.
        // Attempt number and fallback state must still track the switch.
        var llm = new FailoverLlmClient(
            failOnModels: ["model-a"],
            succeedOnModels: ["model-b"]);
        var tools = new ToolRegistry();
        var observer = new FailoverSpyObserver();
        var runtime = new AgentRuntime(
            llm,
            tools,
            observers: [observer],
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test provenance",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelExecutionInfo] = new ModelExecutionInfo
                {
                    Model = new ModelDefinition { Name = "model-a", ProviderKey = "test" },
                    Profile = new CapabilityProfile { Id = "text", Name = "Text" },
                    Attempt = 1
                },
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — LlmCallInfo carries the chain attempt number.
        result.Success.Should().BeTrue();
        observer.LlmCallCompletedInfos.Should().HaveCount(2);
        observer.LlmCallCompletedInfos[0].Attempt.Should().Be(1);
        observer.LlmCallCompletedInfos[0].IsFallback.Should().BeFalse();
        observer.LlmCallCompletedInfos[1].Attempt.Should().Be(2);
        observer.LlmCallCompletedInfos[1].IsFallback.Should().BeTrue();

        // Context provenance is updated after the switch.
        var execInfo = observer.FinalContext!
            .GetProperty<ModelExecutionInfo>(AgentPropertyKeys.ModelExecutionInfo);
        execInfo.Should().NotBeNull();
        execInfo!.Model.Name.Should().Be("model-b");
        execInfo.IsFallback.Should().BeTrue();
        execInfo.Attempt.Should().Be(2);
        execInfo.RemainingFallbacks.Should().Be(0);
        execInfo.SelectionReason.Should().Contain("runtime failover");
    }

    // --- Helpers ---

    private static DefaultModelCatalog CreateTextCatalog() => new(
        new Dictionary<string, IReadOnlyList<ModelDefinition>>
        {
            ["text"] =
            [
                new ModelDefinition { Name = "model-a", ProviderKey = "test" },
                new ModelDefinition { Name = "model-b", ProviderKey = "test" }
            ]
        });

    private static CapabilityProfile TextProfile() => new()
    {
        Id = "text",
        Name = "Text",
        Priority = 10,
        Capabilities = new Capabilities
        {
            CapabilityFlags = EModelCapability.TextGeneration
        }
    };

    // --- Test doubles ---

    private sealed class FailoverLlmClient : ILlmClient
    {
        private readonly HashSet<string> _failOnModels;

        private readonly HashSet<string> _succeedOnModels;

        public List<(string Model, IReadOnlyList<LlmMessage> Messages)> CallLog { get; } = [];

        public FailoverLlmClient(
            IEnumerable<string> failOnModels,
            IEnumerable<string> succeedOnModels)
        {
            _failOnModels = new HashSet<string>(failOnModels, StringComparer.OrdinalIgnoreCase);
            _succeedOnModels = new HashSet<string>(succeedOnModels, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var model = options?.Model ?? "unknown";
            CallLog.Add((model, messages));

            if (_failOnModels.Contains(model))
            {
                // Simulate per-turn timeout (delay until CTS fires).
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return new LlmResponse("never"); // unreachable
            }

            if (_succeedOnModels.Contains(model))
            {
                return new LlmResponse($"response from {model}")
                {
                    Usage = new LlmTokenUsage(10, 5)
                };
            }

            return new LlmResponse($"response from {model}");
        }
    }

    private sealed class FailoverSpyObserver : IAgentObserver
    {
        public IAgentContext? FinalContext { get; private set; }

        public List<LlmCallInfo> LlmCallCompletedInfos { get; } = [];

        public List<(string From, string To, string Reason)> ModelSwitches { get; } = [];

        public Task OnGateRejectedAsync(
            IAgentQualityGate gate, QualityGateResult result, int retryCount,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnLlmCalledAsync(
            IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task OnLlmCallCompletedAsync(LlmCallInfo info, CancellationToken cancellationToken)
        {
            LlmCallCompletedInfos.Add(info);
            return Task.CompletedTask;
        }

        public Task OnLlmRespondedAsync(
            LlmResponse response, TimeSpan duration, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task OnModelSwitchedAsync(
            string fromModel, string toModel, string reason, CancellationToken cancellationToken)
        {
            ModelSwitches.Add((fromModel, toModel, reason));
            return Task.CompletedTask;
        }

        public Task OnPolicyBlockedAsync(
            IAgentPolicy policy, PolicyResult result, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task OnRunCompletedAsync(
            AgentResult result, IAgentContext context, CancellationToken cancellationToken)
        {
            FinalContext = context;
            return Task.CompletedTask;
        }

        public Task OnRunStartedAsync(
            AgentRequest request, IAgentContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task OnToolCompletedAsync(
            ITool tool, ToolResult result, TimeSpan duration, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task OnToolInvokedAsync(
            ITool tool, ToolInvocation invocation, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ScriptedFailoverLlmClient : ILlmClient
    {
        private readonly Queue<Func<IReadOnlyList<LlmMessage>, LlmCompletionOptions?, CancellationToken, Task<LlmResponse>>> _scripts;

        public ScriptedFailoverLlmClient(
            params Func<IReadOnlyList<LlmMessage>, LlmCompletionOptions?, CancellationToken, Task<LlmResponse>>[] scripts)
        {
            _scripts = new Queue<Func<IReadOnlyList<LlmMessage>, LlmCompletionOptions?, CancellationToken, Task<LlmResponse>>>(
                scripts);
        }

        public List<(string Model, IReadOnlyList<LlmMessage> Messages)> CallLog { get; } = [];

        public async Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            // Snapshot the conversation — the loop reuses the same list.
            CallLog.Add((options?.Model ?? "unknown", messages.ToList()));
            return await _scripts.Dequeue()(messages, options, cancellationToken);
        }
    }

    private sealed class EchoTool : ITool
    {
        private readonly string _output;

        public EchoTool(string output) => _output = output;

        public ToolDefinition Definition => new("echo", "Echoes input for tests.");

        public string Description => "Echoes input for tests.";

        public string Name => "echo";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ToolResult(true, _output));
    }

    private sealed class StateCapturingMiddleware : IAgentPipelineMiddleware
    {
        public int? FinalTurnCount { get; private set; }

        public string Name => "StateCapture";

        public async Task<AgentResult> InvokeAsync(
            IExecutionContext context,
            AgentPipelineDelegate next)
        {
            var result = await next(context);
            FinalTurnCount = context.State.TurnCount;
            return result;
        }
    }
}
