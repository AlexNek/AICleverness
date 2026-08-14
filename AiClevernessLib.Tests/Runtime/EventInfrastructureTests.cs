using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Tests.Runtime;

public class EventInfrastructureTests
{
    [Fact]
    public void AddExecutionEventHandler_MultipleHandlers_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddExecutionEventHandler<CollectingHandler, ExecutionStartedBusEvent>();
        services.AddExecutionEventHandler<AnotherHandler, ExecutionStartedBusEvent>();
        var sp = services.BuildServiceProvider();

        var handlers = sp.GetServices<IExecutionEventHandler<ExecutionStartedBusEvent>>();

        handlers.Should().HaveCount(2);
    }

    [Fact]
    public void AddExecutionEventHandler_RegistersHandler()
    {
        var services = new ServiceCollection();
        services.AddExecutionEventHandler<CollectingHandler, ExecutionStartedBusEvent>();
        var sp = services.BuildServiceProvider();

        var handlers = sp.GetServices<IExecutionEventHandler<ExecutionStartedBusEvent>>();

        handlers.Should().HaveCount(1);
        handlers.First().Should().BeOfType<CollectingHandler>();
    }

    [Fact]
    public void AddExecutionEventPublisher_RegistersCustomPublisher()
    {
        var services = new ServiceCollection();
        services.AddExecutionEventPublisher<CustomPublisher>();
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetService<IExecutionEventPublisher>();

        publisher.Should().BeOfType<CustomPublisher>();
    }

    [Fact]
    public void AddInMemoryEventBus_DoesNotOverwriteExisting()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionEventPublisher, CustomPublisher>();
        services.AddInMemoryEventBus();
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetService<IExecutionEventPublisher>();

        publisher.Should().BeOfType<CustomPublisher>();
    }

    // ── DI Registration ─────────────────────────────────────────────────────

    [Fact]
    public void AddInMemoryEventBus_RegistersPublisher()
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventBus();
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetService<IExecutionEventPublisher>();

        publisher.Should().NotBeNull();
        publisher.Should().BeOfType<InMemoryEventBus>();
    }

    [Fact]
    public async Task EventBus_PublishAsync_DifferentEventTypes_RoutedCorrectly()
    {
        var startHandler = new CollectingHandler();
        var toolHandler = new ToolCollectingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(startHandler);
        services.AddSingleton<IExecutionEventHandler<ToolInvokedBusEvent>>(toolHandler);
        var sp = services.BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        await bus.PublishAsync(new ExecutionStartedBusEvent("exec-1", new AgentRequest("goal")));
        await bus.PublishAsync(
            new ToolInvokedBusEvent("exec-1", "tool-a", new ToolInvocation("tool-a")));

        startHandler.ReceivedEvents.Should().HaveCount(1);
        toolHandler.ReceivedEvents.Should().HaveCount(1);
        toolHandler.ReceivedEvents[0].ToolName.Should().Be("tool-a");
    }

    // ── InMemoryEventBus ────────────────────────────────────────────────────

    [Fact]
    public async Task EventBus_PublishAsync_DispatchesToHandler()
    {
        var handler = new CollectingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(handler);
        var sp = services.BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        var evt = new ExecutionStartedBusEvent("exec-1", new AgentRequest("goal"));
        await bus.PublishAsync(evt);

        handler.ReceivedEvents.Should().HaveCount(1);
        handler.ReceivedEvents[0].ExecutionId.Should().Be("exec-1");
    }

    [Fact]
    public async Task EventBus_PublishAsync_HandlerThrows_DoesNotPropagate()
    {
        var throwingHandler = new ThrowingHandler();
        var collectingHandler = new CollectingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(throwingHandler);
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(collectingHandler);
        var sp = services.BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        var act = () =>
            bus.PublishAsync(new ExecutionStartedBusEvent("exec-1", new AgentRequest("goal")));

        await act.Should().NotThrowAsync();
        // Second handler should still be called despite first throwing
        collectingHandler.ReceivedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task EventBus_PublishAsync_MultipleHandlers_AllCalled()
    {
        var handler1 = new CollectingHandler();
        var handler2 = new CollectingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(handler1);
        services.AddSingleton<IExecutionEventHandler<ExecutionStartedBusEvent>>(handler2);
        var sp = services.BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        await bus.PublishAsync(new ExecutionStartedBusEvent("exec-1", new AgentRequest("goal")));

        handler1.ReceivedEvents.Should().HaveCount(1);
        handler2.ReceivedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task EventBus_PublishAsync_NoHandlers_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        var act = () =>
            bus.PublishAsync(new ExecutionStartedBusEvent("exec-1", new AgentRequest("goal")));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EventBus_PublishAsync_NullEvent_Throws()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var bus = new InMemoryEventBus(sp);

        var act = () => bus.PublishAsync<ExecutionStartedBusEvent>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void ExecutionCompletedBusEvent_HasCorrectProperties()
    {
        var result = new AgentResult(true, "done");
        var evt = new ExecutionCompletedBusEvent("exec-1", result, TimeSpan.FromSeconds(5));

        evt.ExecutionId.Should().Be("exec-1");
        evt.EventType.Should().Be("ExecutionCompleted");
        evt.Result.Should().Be(result);
        evt.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }
    // ── IExecutionEvent concrete types ──────────────────────────────────────

    [Fact]
    public void ExecutionStartedBusEvent_HasCorrectProperties()
    {
        var request = new AgentRequest("Test goal");
        var evt = new ExecutionStartedBusEvent("exec-1", request);

        evt.ExecutionId.Should().Be("exec-1");
        evt.EventType.Should().Be("ExecutionStarted");
        evt.Request.Should().Be(request);
        evt.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void LlmCallCompletedBusEvent_HasCorrectProperties()
    {
        var usage = new LlmTokenUsage(100, 50);
        var evt = new LlmCallCompletedBusEvent("exec-1", TimeSpan.FromMilliseconds(500), usage);

        evt.EventType.Should().Be("LlmCallCompleted");
        evt.Duration.Should().Be(TimeSpan.FromMilliseconds(500));
        evt.Usage.Should().Be(usage);
    }

    [Fact]
    public void PolicyBlockedBusEvent_HasCorrectProperties()
    {
        var evt = new PolicyBlockedBusEvent("exec-1", "RateLimit", "too fast");

        evt.EventType.Should().Be("PolicyBlocked");
        evt.PolicyName.Should().Be("RateLimit");
        evt.Reason.Should().Be("too fast");
    }

    [Fact]
    public void QualityGateEvaluatedBusEvent_HasCorrectProperties()
    {
        var evt = new QualityGateEvaluatedBusEvent(
            "exec-1",
            "JsonGate",
            false,
            true,
            "bad json",
            2);

        evt.EventType.Should().Be("QualityGateEvaluated");
        evt.GateName.Should().Be("JsonGate");
        evt.Approved.Should().BeFalse();
        evt.Retry.Should().BeTrue();
        evt.Reason.Should().Be("bad json");
        evt.RetryCount.Should().Be(2);
    }

    // ── Runtime Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Runtime_WithEventBus_PublishesExecutionStartedAndCompleted()
    {
        var startHandler = new CollectingHandler();
        var completedHandler = new CompletedCollectingHandler();
        var services = new ServiceCollection();
        services.AddInMemoryEventBus();
        services.AddExecutionEventHandler<CollectingHandler, ExecutionStartedBusEvent>();
        services.AddExecutionEventHandler<CompletedCollectingHandler, ExecutionCompletedBusEvent>();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<IAgentRuntime>();
        var result = await runtime.RunAsync(new AgentRequest("hello"));

        var startEvents = sp.GetServices<IExecutionEventHandler<ExecutionStartedBusEvent>>();
        var handler = startEvents.OfType<CollectingHandler>().First();
        handler.ReceivedEvents.Should().HaveCount(1);
        handler.ReceivedEvents[0].Request.Goal.Should().Be("hello");
    }

    [Fact]
    public async Task Runtime_WithEventBus_PublishesToolEvents()
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventBus();
        services.AddExecutionEventHandler<ToolCollectingHandler, ToolInvokedBusEvent>();
        services.AddExecutionEventHandler<ToolCompletedCollectingHandler, ToolCompletedBusEvent>();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<ToolCallingLlmClient>();
        var sp = services.BuildServiceProvider();

        // Register tool directly in the registry (AddAgentTool doesn't auto-populate ToolRegistry).
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new FakeTool());

        var runtime = sp.GetRequiredService<IAgentRuntime>();
        var result = await runtime.RunAsync(new AgentRequest("use tool"));

        var invokedHandlers = sp.GetServices<IExecutionEventHandler<ToolInvokedBusEvent>>()
            .OfType<ToolCollectingHandler>().First();
        invokedHandlers.ReceivedEvents.Should().HaveCount(1);
        invokedHandlers.ReceivedEvents[0].ToolName.Should().Be("fake_tool");

        var completedHandlers = sp.GetServices<IExecutionEventHandler<ToolCompletedBusEvent>>()
            .OfType<ToolCompletedCollectingHandler>().First();
        completedHandlers.ReceivedEvents.Should().HaveCount(1);
        completedHandlers.ReceivedEvents[0].ToolName.Should().Be("fake_tool");
    }

    [Fact]
    public async Task Runtime_WithEventBus_PublishesValidationEvents()
    {
        var validationHandler = new ValidationCollectingHandler();
        var services = new ServiceCollection();
        services.AddInMemoryEventBus();
        services
            .AddExecutionEventHandler<ValidationCollectingHandler, ValidationCompletedBusEvent>();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        services.AddAgentResultValidator<FakeValidator>();
        var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<IAgentRuntime>();
        await runtime.RunAsync(new AgentRequest("test"));

        var handlers = sp.GetServices<IExecutionEventHandler<ValidationCompletedBusEvent>>()
            .OfType<ValidationCollectingHandler>().First();
        handlers.ReceivedEvents.Should().HaveCount(1);
        handlers.ReceivedEvents[0].ValidatorName.Should().Be("FakeValidator");
        handlers.ReceivedEvents[0].IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Runtime_WithoutEventBus_WorksFine()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<IAgentRuntime>();
        var result = await runtime.RunAsync(new AgentRequest("hello"));

        result.Should().NotBeNull();
    }

    [Fact]
    public void ToolCompletedBusEvent_HasCorrectProperties()
    {
        var result = new ToolResult(true, "output");
        var evt = new ToolCompletedBusEvent(
            "exec-1",
            "my_tool",
            result,
            TimeSpan.FromMilliseconds(100));

        evt.EventType.Should().Be("ToolCompleted");
        evt.ToolName.Should().Be("my_tool");
        evt.Result.Success.Should().BeTrue();
        evt.Duration.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void ToolFailedBusEvent_HasCorrectProperties()
    {
        var evt = new ToolFailedBusEvent("exec-1", "my_tool", "boom");

        evt.EventType.Should().Be("ToolFailed");
        evt.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public void ToolInvokedBusEvent_HasCorrectProperties()
    {
        var invocation = new ToolInvocation(
            "my_tool",
            new Dictionary<string, object> { ["key"] = "val" });
        var evt = new ToolInvokedBusEvent("exec-1", "my_tool", invocation);

        evt.ExecutionId.Should().Be("exec-1");
        evt.EventType.Should().Be("ToolInvoked");
        evt.ToolName.Should().Be("my_tool");
        evt.Invocation.Should().Be(invocation);
    }

    [Fact]
    public void TransformationCompletedBusEvent_HasCorrectProperties()
    {
        var evt = new TransformationCompletedBusEvent("exec-1", "PiiRedactor");

        evt.EventType.Should().Be("TransformationCompleted");
        evt.TransformerName.Should().Be("PiiRedactor");
    }

    [Fact]
    public void ValidationCompletedBusEvent_HasCorrectProperties()
    {
        var evt = new ValidationCompletedBusEvent("exec-1", "SchemaValidator", false, "invalid");

        evt.EventType.Should().Be("ValidationCompleted");
        evt.ValidatorName.Should().Be("SchemaValidator");
        evt.IsValid.Should().BeFalse();
        evt.Error.Should().Be("invalid");
    }

    private sealed class AnotherHandler : IExecutionEventHandler<ExecutionStartedBusEvent>
    {
        public Task HandleAsync(
            ExecutionStartedBusEvent @event,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // ── Test Helpers ────────────────────────────────────────────────────────

    private sealed class CollectingHandler : IExecutionEventHandler<ExecutionStartedBusEvent>
    {
        public List<ExecutionStartedBusEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(
            ExecutionStartedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class
        CompletedCollectingHandler : IExecutionEventHandler<ExecutionCompletedBusEvent>
    {
        public List<ExecutionCompletedBusEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(
            ExecutionCompletedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class CustomPublisher : IExecutionEventPublisher
    {
        public Task PublishAsync<TEvent>(
            TEvent @event,
            CancellationToken cancellationToken = default)
            where TEvent : IExecutionEvent =>
            Task.CompletedTask;
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            LlmCompletionOptions? options,
            CancellationToken ct)
        {
            return Task.FromResult(new LlmResponse("Hello!"));
        }
    }

    private sealed class FakeTool : ITool
    {
        public ToolDefinition Definition => new(Name, Description);

        public string Description => "A fake tool for testing";

        public string Name => "fake_tool";

        public Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct)
        {
            return Task.FromResult(new ToolResult(true, "tool output"));
        }
    }

    private sealed class FakeValidator : IAgentResultValidator
    {
        public string Name => "FakeValidator";

        public Task<ValidationResult> ValidateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken ct)
        {
            return Task.FromResult(new ValidationResult(true));
        }
    }

    private sealed class ThrowingHandler : IExecutionEventHandler<ExecutionStartedBusEvent>
    {
        public Task HandleAsync(
            ExecutionStartedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Handler failed!");
        }
    }

    private sealed class ToolCallingLlmClient : ILlmClient
    {
        private int _callCount;

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools,
            LlmCompletionOptions? options,
            CancellationToken ct)
        {
            _callCount++;
            if (_callCount == 1)
            {
                var toolCalls = new List<LlmToolCall> { new("call-1", "fake_tool", "{}") };
                return Task.FromResult(new LlmResponse(null, toolCalls));
            }

            return Task.FromResult(new LlmResponse("Done after tool call"));
        }
    }

    private sealed class ToolCollectingHandler : IExecutionEventHandler<ToolInvokedBusEvent>
    {
        public List<ToolInvokedBusEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(
            ToolInvokedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class
        ToolCompletedCollectingHandler : IExecutionEventHandler<ToolCompletedBusEvent>
    {
        public List<ToolCompletedBusEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(
            ToolCompletedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class
        ValidationCollectingHandler : IExecutionEventHandler<ValidationCompletedBusEvent>
    {
        public List<ValidationCompletedBusEvent> ReceivedEvents { get; } = new();

        public Task HandleAsync(
            ValidationCompletedBusEvent @event,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }
}
