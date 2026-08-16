using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

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
        result.Reasoning.Should().Contain("model-c");
        result.Reasoning.Should().Contain("failover chain exhausted");
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
        result.Reasoning.Should().Contain("timed out");
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
        observer.LlmCallCompletedInfos[0].Classification.Should().Be(FailureClassification.TransientAdvance);
        observer.LlmCallCompletedInfos[1].Success.Should().BeTrue();
        observer.LlmCallCompletedInfos[1].Model.Should().Be("model-b");

        // OnModelSwitchedAsync fires once.
        observer.ModelSwitches.Should().HaveCount(1);
        observer.ModelSwitches[0].From.Should().Be("model-a");
        observer.ModelSwitches[0].To.Should().Be("model-b");
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

        result.Should().Be(FailureClassification.TransientAdvance);
    }

    [Fact]
    public void DefaultLlmErrorClassifier_CallerCancellation_ReturnsPermanent()
    {
        var classifier = new DefaultLlmErrorClassifier();
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        var ex = new OperationCanceledException();

        var result = classifier.Classify(ex, callerCts.Token);

        result.Should().Be(FailureClassification.Permanent);
    }

    [Fact]
    public void DefaultLlmErrorClassifier_OtherException_ReturnsPermanent()
    {
        var classifier = new DefaultLlmErrorClassifier();
        using var callerCts = new CancellationTokenSource();
        var ex = new InvalidOperationException("something broke");

        var result = classifier.Classify(ex, callerCts.Token);

        result.Should().Be(FailureClassification.Permanent);
    }

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
            => Task.CompletedTask;

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
}
