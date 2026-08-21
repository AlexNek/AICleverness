using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using AiClevernessLib.Tests.Testing;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class StreamingRuntimeTests
{
    [Fact]
    public async Task RunStreamingAsync_AllEventsHaveExecutionId()
    {
        var llm = new FakeLlmClient([new LlmResponse("hi")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("test");

        var events = await CollectEvents(runtime, request);

        events.Should().AllSatisfy(e => { e.ExecutionId.Should().NotBeNullOrWhiteSpace(); });

        // All events in same execution should share the same id
        var ids = events.Select(e => e.ExecutionId).Distinct().ToList();
        ids.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunStreamingAsync_Cancellation_EmitsCancellationEvent()
    {
        var llm = new SlowLlmClient();
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions { DefaultCompletionTimeoutSeconds = 30 });
        var request = new AgentRequest("slow");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Enumeration must complete without throwing: cancellation is surfaced as a
        // CancellationEvent followed by a failed RunCompletedEvent.
        var events = await CollectEvents(runtime, request, cts.Token);

        events.Should().ContainSingle(e => e is RunStartedEvent);
        events.Should().ContainSingle(e => e is CancellationEvent);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
        completed.Result.FailureKind.Should().Be(EFailureKind.Cancelled);
        events.Last().Should().Be(completed);
    }

    [Fact]
    public async Task RunStreamingAsync_Cancellation_CompletesCleanly_WithObserverAndPublisher()
    {
        var llm = new SlowLlmClient();
        var tools = new ToolRegistry();
        var observer = new RecordingObserver();
        var publisher = new RecordingPublisher();
        var runtime = new AgentRuntime(
            llm,
            tools,
            observers: [observer],
            eventPublisher: publisher,
            options: new AgentRuntimeOptions { DefaultCompletionTimeoutSeconds = 30 });
        var request = new AgentRequest("slow");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Finalization after cancellation must not be cancellable: observers that
        // await with the token they receive must not break the stream.
        var events = await CollectEvents(runtime, request, cts.Token);

        events.Should().ContainSingle(e => e is CancellationEvent);
        events.Should().ContainSingle(e => e is RunCompletedEvent);
        observer.CompletedRuns.Should().Be(1);
        publisher.PublishedEvents.Should().ContainSingle(e => e is ExecutionCompletedBusEvent);
    }

    [Fact]
    public async Task RunStreamingAsync_CancellationDuringToolExecution_EmitsCancellationEvent()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse(null, [new LlmToolCall("c1", "slow", "{}")])
            ]);
        var tools = new ToolRegistry();
        tools.Register(new SlowTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("use slow tool", ["slow"]);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var events = await CollectEvents(runtime, request, cts.Token);

        events.Should().ContainSingle(e => e is ToolStartedEvent);
        events.Should().ContainSingle(e => e is CancellationEvent);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
        completed.Result.Reasoning.Should()
            .Contain("Cancellation requested during tool execution");
    }

    [Fact]
    public async Task RunStreamingAsync_EmitsModelChunkEvent()
    {
        var llm = new FakeLlmClient([new LlmResponse("the answer")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("question");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is ModelChunkEvent);
        var chunk = events.OfType<ModelChunkEvent>().Single();
        chunk.Content.Should().Be("the answer");
        chunk.IsFinal.Should().BeTrue();
        chunk.Turn.Should().Be(0);
    }

    [Fact]
    public async Task RunStreamingAsync_EmitsRunStartedAndRunCompleted()
    {
        var llm = new FakeLlmClient([new LlmResponse("hello")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Say hello");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is RunStartedEvent);
        events.Should().ContainSingle(e => e is RunCompletedEvent);

        var started = events.OfType<RunStartedEvent>().Single();
        started.Request.Should().BeSameAs(request);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeTrue();
        completed.Result.Output.Should().Be("hello");
        completed.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunStreamingAsync_EmitsTurnStartedEvent()
    {
        var llm = new FakeLlmClient([new LlmResponse("answer")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("question");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is TurnStartedEvent);
        var turnEvent = events.OfType<TurnStartedEvent>().Single();
        turnEvent.Turn.Should().Be(0);
    }

    [Fact]
    public async Task RunStreamingAsync_EventOrderIsCorrect()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse(null, [new LlmToolCall("c1", "echo", "{\"message\":\"x\"}")]),
                new LlmResponse("final")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest(
            "test",
                ["echo"],
            new Dictionary<string, object> { [AgentPropertyKeys.MaxTurns] = 5 });

        var events = await CollectEvents(runtime, request);
        var types = events.Select(e => e.EventType).ToList();

        types[0].Should().Be("RunStarted");
        types[1].Should().Be("TurnStarted");
        types.Should().Contain("ToolStarted");
        types.Should().Contain("ToolCompleted");
        types.Should().Contain("ModelChunk");
        types.Last().Should().Be("RunCompleted");
    }

    [Fact]
    public async Task RunStreamingAsync_ExhaustedTurns_EmitsRunCompletedWithFailure()
    {
        // LLM returns empty content each turn
        var llm = new FakeLlmClient([new LlmResponse(null), new LlmResponse(null)]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions { DefaultMaxTurns = 2 });
        var request = new AgentRequest("empty");

        var events = await CollectEvents(runtime, request);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
        completed.Result.FailureKind.Should().Be(EFailureKind.TurnLimitExceeded);
    }

    [Fact]
    public async Task RunStreamingAsync_LlmException_EmitsFailureEvent()
    {
        var llm = new ExplodingLlmClient();
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("explode");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is FailureEvent);
        var failure = events.OfType<FailureEvent>().Single();
        failure.Phase.Should().Be("LlmCompletion");
        failure.IsTransient.Should().BeFalse();
        failure.Error.Should().Contain("Kaboom");
    }

    [Fact]
    public async Task RunStreamingAsync_LlmTimeout_EmitsFailureEvent()
    {
        var llm = new TimeoutLlmClient();
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions { DefaultCompletionTimeoutSeconds = 1 });
        var request = new AgentRequest("timeout test");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is FailureEvent);
        var failure = events.OfType<FailureEvent>().Single();
        failure.Phase.Should().Be("LlmCompletion");
        failure.IsTransient.Should().BeTrue();

        events.Should().ContainSingle(e => e is RunCompletedEvent);
        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RunStreamingAsync_PolicyBlocked_EmitsPolicyBlockedAndRunCompleted()
    {
        var llm = new FakeLlmClient([new LlmResponse("nope")]);
        var tools = new ToolRegistry();
        var policy = new BlockAllPolicy();
        var runtime = new AgentRuntime(llm, tools, policies: [policy]);
        var request = new AgentRequest("blocked");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is PolicyBlockedAgentEvent);
        var blocked = events.OfType<PolicyBlockedAgentEvent>().Single();
        blocked.PolicyName.Should().Be("BlockAllPolicy");

        events.Should().ContainSingle(e => e is RunCompletedEvent);
        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RunStreamingAsync_WithToolCall_EmitsToolEvents()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse(null, [new LlmToolCall("c1", "echo", "{\"message\":\"hi\"}")]),
                new LlmResponse("done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest(
            "use echo",
                ["echo"],
            new Dictionary<string, object> { [AgentPropertyKeys.MaxTurns] = 3 });

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is ToolStartedEvent);
        events.Should().ContainSingle(e => e is ToolCompletedAgentEvent);

        var toolStarted = events.OfType<ToolStartedEvent>().Single();
        toolStarted.ToolName.Should().Be("echo");

        var toolCompleted = events.OfType<ToolCompletedAgentEvent>().Single();
        toolCompleted.ToolName.Should().Be("echo");
        toolCompleted.Result.Success.Should().BeTrue();
        toolCompleted.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunStreamingAsync_CachedToolResult_SuppressesRealToolEventsAndPreservesOutput()
    {
        // Arrange
        var llm = new FakeChatClient()
            .EnqueueToolCallResponse(new LlmToolCall("c1", "echo", "{\"message\":\"hi\"}"))
            .EnqueueResponse("done");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var executor = new CacheHitToolExecutor();
        var runtime = new AgentRuntime(llm, tools, toolExecutor: executor);
        var request = new AgentRequest("use cached echo", ["echo"]);

        // Act
        var events = await CollectEvents(runtime, request);

        // Assert
        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeTrue();
        completed.Result.Output.Should().Be("done");
        completed.Result.Steps.Should().Contain("  echo reused cached result: cached output");
        events.Should().NotContain(e => e is ToolStartedEvent);
        events.Should().NotContain(e => e is ToolCompletedAgentEvent);
        executor.ExecuteCalled.Should().BeFalse();
        llm.Calls.Should().HaveCount(2);
        llm.Calls[1].Messages.Should()
            .Contain(message => message.Role == "tool" && message.Content == "cached output");
    }

    [Fact]
    public async Task RunStreamingAsync_PublishesBusEvents_WhenPublisherRegistered()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var publisher = new RecordingPublisher();
        var runtime = new AgentRuntime(llm, tools, eventPublisher: publisher);
        var request = new AgentRequest("publish");

        var events = await CollectEvents(runtime, request);

        events.Should().ContainSingle(e => e is RunCompletedEvent);
        publisher.PublishedEvents.Should().ContainSingle(e => e is ExecutionStartedBusEvent);
        publisher.PublishedEvents.Should().ContainSingle(e => e is LlmCallCompletedBusEvent);
        publisher.PublishedEvents.Should().ContainSingle(e => e is ExecutionCompletedBusEvent);
    }

    [Fact]
    public async Task RunStreamingAsync_PublishesLlmCallCompleted_OnFailedAttempt()
    {
        var llm = new ThrowingLlmClient(new InvalidOperationException("boom"));
        var tools = new ToolRegistry();
        var publisher = new RecordingPublisher();
        var runtime = new AgentRuntime(llm, tools, eventPublisher: publisher);
        var request = new AgentRequest("publish failure");

        await CollectEvents(runtime, request);

        // Failed attempts are published too, carrying outcome and turn.
        var completed = publisher.PublishedEvents
            .OfType<LlmCallCompletedBusEvent>()
            .Single();
        completed.Success.Should().BeFalse();
        completed.Error.Should().Be("boom");
        completed.Turn.Should().Be(0);
        completed.Usage.Should().BeNull();
    }

    [Fact]
    public async Task RunStreamingAsync_QualityGateRejection_ReturnsFailedResult()
    {
        var llm = new FakeLlmClient([new LlmResponse("mediocre answer")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools, qualityGates: [new AlwaysRejectGate()]);
        var request = new AgentRequest("answer");

        var events = await CollectEvents(runtime, request);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeFalse();
        completed.Result.Reasoning.Should().Be("Not good enough.");
    }

    [Fact]
    public async Task RunStreamingAsync_QualityGateRetry_RerunsLoopAndSucceeds()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse("bad answer"),
                new LlmResponse("good answer")
            ]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools, qualityGates: [new RequiresGoodAnswerGate()]);
        var request = new AgentRequest(
            "Answer well",
            Parameters:
            new Dictionary<string, object> { [AgentPropertyKeys.MaxQualityRetries] = 1 });

        var events = await CollectEvents(runtime, request);

        var completed = events.OfType<RunCompletedEvent>().Single();
        completed.Result.Success.Should().BeTrue();
        completed.Result.Output.Should().Be("good answer");
    }

    [Fact]
    public async Task RunStreamingAsync_RunsRegisteredUserMiddleware()
    {
        var llm = new FakeLlmClient([new LlmResponse("ok")]);
        var tools = new ToolRegistry();
        var middleware = new TrackingMiddleware();
        var runtime = new AgentRuntime(llm, tools, middleware: [middleware]);
        var request = new AgentRequest("middleware");

        var events = await CollectEvents(runtime, request);

        middleware.Invoked.Should().BeTrue();
        events.Should().ContainSingle(e => e is RunCompletedEvent);
    }

    private sealed class AlwaysRejectGate : IAgentQualityGate
    {
        public string Name => "AlwaysReject";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<QualityGateResult> EvaluateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new QualityGateResult(false, false, "Not good enough."));
        }
    }

    private sealed class BlockAllPolicy : IAgentPolicy
    {
        public string Name => "BlockAllPolicy";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<PolicyResult> EvaluateAsync(
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new PolicyResult(true, 0.0, "block", "BlockAllPolicy blocked everything."));
        }
    }

    private static async Task<List<AgentEvent>> CollectEvents(
        IStreamingAgentRuntime runtime,
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var events = new List<AgentEvent>();
        await foreach (var e in runtime.RunStreamingAsync(request, cancellationToken))
        {
            events.Add(e);
        }

        return events;
    }

    private sealed class EchoTool : ITool
    {
        public ToolDefinition Definition =>
            new(
                Name,
                Description,
                """
                {
                    "type": "object",
                    "properties": { "message": { "type": "string" } },
                    "required": ["message"]
                }
                """);

        public string Description => "Echoes a message.";

        public string Name => "echo";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            var msg = invocation.Arguments.TryGetValue("message", out var m) ? m?.ToString() : null;
            return Task.FromResult(new ToolResult(true, msg ?? "(empty)"));
        }
    }

    private sealed class CacheHitToolExecutor : IToolExecutor, ICacheAwareToolExecutor
    {
        public bool ExecuteCalled { get; private set; }

        public bool TryGetCachedResult(
            ITool tool,
            ToolInvocation invocation,
            out ToolResult result)
        {
            result = new ToolResult(true, "cached output");
            return true;
        }

        public Task<ToolResult> ExecuteAsync(
            ITool tool,
            ToolInvocation invocation,
            ToolExecutionPolicy policy,
            CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            throw new InvalidOperationException("A cache hit must not execute the tool.");
        }
    }

    private sealed class ExplodingLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Kaboom!");
        }
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly Queue<LlmResponse> _responses;

        public FakeLlmClient(IEnumerable<LlmResponse> responses)
        {
            _responses = new Queue<LlmResponse>(responses);
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = _responses.Dequeue();
            return Task.FromResult(response with { Usage = new LlmTokenUsage(10, 5) });
        }
    }

    private sealed class ThrowingLlmClient : ILlmClient
    {
        private readonly Exception _exception;

        public ThrowingLlmClient(Exception exception) => _exception = exception;

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class RecordingObserver : IAgentObserver
    {
        public int CompletedRuns { get; private set; }

        public Task OnGateRejectedAsync(
            IAgentQualityGate gate,
            QualityGateResult result,
            int retryCount,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public Task OnLlmCalledAsync(
            IReadOnlyList<LlmMessage> messages,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public Task OnLlmRespondedAsync(
            LlmResponse response,
            TimeSpan duration,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public Task OnPolicyBlockedAsync(
            IAgentPolicy policy,
            PolicyResult result,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public async Task OnRunCompletedAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            await WaitAsync(cancellationToken);
            CompletedRuns++;
        }

        public Task OnRunStartedAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public Task OnToolCompletedAsync(
            ITool tool,
            ToolResult result,
            TimeSpan duration,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        public Task OnToolInvokedAsync(
            ITool tool,
            ToolInvocation invocation,
            CancellationToken cancellationToken) =>
            WaitAsync(cancellationToken);

        // Simulates an observer that awaits with the token it is given.
        private static Task WaitAsync(CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
    }

    private sealed class RecordingPublisher : IExecutionEventPublisher
    {
        public List<IExecutionEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<TEvent>(
            TEvent @event,
            CancellationToken cancellationToken = default)
            where TEvent : IExecutionEvent
        {
            PublishedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RequiresGoodAnswerGate : IAgentQualityGate
    {
        public string Name => "RequiresGoodAnswer";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<QualityGateResult> EvaluateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            var approved = string.Equals(result.Output, "good answer", StringComparison.Ordinal);
            return Task.FromResult(
                new QualityGateResult(approved, !approved, "Expected good answer."));
        }
    }

    private sealed class SlowLlmClient : ILlmClient
    {
        public async Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new LlmResponse("slow");
        }
    }

    private sealed class SlowTool : ITool
    {
        public ToolDefinition Definition =>
            new(
                Name,
                Description,
                """
                {
                    "type": "object",
                    "properties": {}
                }
                """);

        public string Description => "Waits until cancelled.";

        public string Name => "slow";

        public async Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new ToolResult(true, "done");
        }
    }

    private sealed class TimeoutLlmClient : ILlmClient
    {
        public async Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new LlmResponse("never");
        }
    }

    private sealed class TrackingMiddleware : IAgentPipelineMiddleware
    {
        public bool Invoked { get; private set; }

        public string Name => "Tracking";

        public Task<AgentResult> InvokeAsync(
            IExecutionContext context,
            AgentPipelineDelegate next)
        {
            Invoked = true;
            return next(context);
        }
    }
}
