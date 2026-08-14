using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Filtering;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class AgentScopingTests
{
    [Fact]
    public async Task AgentName_DefaultsToDefault_WhenNotSet()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var observer = new NameCapturingObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);

        await runtime.RunAsync(new AgentRequest("test"));

        observer.CapturedAgentName.Should().Be("default");
    }

    [Fact]
    public async Task AgentName_IsSetFromRequest()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var observer = new NameCapturingObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);

        await runtime.RunAsync(new AgentRequest("test", AgentName: "MyAgent"));

        observer.CapturedAgentName.Should().Be("MyAgent");
    }

    [Fact]
    public async Task InputValidator_Global_RunsOnAllAgents()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var validator = new RejectingInputValidator();
        var runtime = new AgentRuntime(llm, tools, inputValidators: [validator]);

        var result = await runtime.RunAsync(new AgentRequest("test", AgentName: "AnyAgent"));

        result.Success.Should().BeFalse();
        result.Reasoning.Should().Contain("always rejected");
    }

    [Fact]
    public async Task InputValidator_Scoped_OnlyRunsOnMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var validator = new FilteredInputValidator(
            new RejectingInputValidator(),
            ctx => ctx.AgentName == "TargetAgent");
        var runtime = new AgentRuntime(llm, tools, inputValidators: [validator]);

        // Should pass — different agent
        var result1 = await runtime.RunAsync(new AgentRequest("test", AgentName: "OtherAgent"));
        result1.Success.Should().BeTrue();

        // Should fail — matching agent
        var result2 = await runtime.RunAsync(new AgentRequest("test", AgentName: "TargetAgent"));
        result2.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Policy_Scoped_OnlyBlocksMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var policy = new FilteredPolicy(
            new BlockAllPolicy(),
            ctx => ctx.AgentName == "RestrictedAgent");
        var runtime = new AgentRuntime(llm, tools, policies: [policy]);

        // Should pass — different agent
        var result1 = await runtime.RunAsync(new AgentRequest("test", AgentName: "FreeAgent"));
        result1.Success.Should().BeTrue();

        // Should be blocked — matching agent
        var result2 =
            await runtime.RunAsync(new AgentRequest("test", AgentName: "RestrictedAgent"));
        result2.Success.Should().BeFalse();
    }

    [Fact]
    public async Task QualityGate_Scoped_OnlyRunsOnMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("output")]);
        var tools = new ToolRegistry();
        var gate = new FilteredQualityGate(
            new AlwaysRejectGate(),
            ctx => ctx.AgentName == "QualityAgent");
        var runtime = new AgentRuntime(llm, tools, qualityGates: [gate]);

        // Should pass — different agent
        var result1 = await runtime.RunAsync(new AgentRequest("test", AgentName: "FastAgent"));
        result1.Success.Should().BeTrue();

        // Should fail — matching agent
        var result2 = await runtime.RunAsync(new AgentRequest("test", AgentName: "QualityAgent"));
        result2.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ResultValidator_Scoped_OnlyRunsOnMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("output")]);
        var tools = new ToolRegistry();
        var validator = new FilteredResultValidator(
            new AlwaysFailResultValidator(),
            ctx => ctx.AgentName == "StrictAgent");
        var runtime = new AgentRuntime(llm, tools, validators: [validator]);

        // Should pass — different agent
        var result1 = await runtime.RunAsync(new AgentRequest("test", AgentName: "RelaxedAgent"));
        result1.Success.Should().BeTrue();

        // Should fail — matching agent
        var result2 = await runtime.RunAsync(new AgentRequest("test", AgentName: "StrictAgent"));
        result2.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Transformer_Scoped_OnlyTransformsMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("hello"), new LlmResponse("hello")]);
        var tools = new ToolRegistry();
        var transformer = new FilteredTransformer(
            new UppercaseTransformer(),
            ctx => ctx.AgentName == "UpperAgent");
        var runtime = new AgentRuntime(llm, tools, transformers: [transformer]);

        // Should NOT transform — different agent
        var result1 = await runtime.RunAsync(new AgentRequest("test", AgentName: "LowerAgent"));
        result1.Output.Should().Be("hello");

        // Should transform — matching agent
        var result2 = await runtime.RunAsync(new AgentRequest("test", AgentName: "UpperAgent"));
        result2.Output.Should().Be("HELLO");
    }

    [Fact]
    public async Task Middleware_Scoped_RunsInnerOnMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var counting = new CountingMiddleware();
        var middleware = new FilteredMiddleware(
            counting,
            ctx => ctx.AgentName == "TargetAgent");
        var runtime = new AgentRuntime(llm, tools, middleware: [middleware]);

        var result = await runtime.RunAsync(new AgentRequest("test", AgentName: "TargetAgent"));

        result.Success.Should().BeTrue();
        counting.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task Middleware_Scoped_SkipsInnerOnNonMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var counting = new CountingMiddleware();
        var middleware = new FilteredMiddleware(
            counting,
            ctx => ctx.AgentName == "TargetAgent");
        var runtime = new AgentRuntime(llm, tools, middleware: [middleware]);

        var result = await runtime.RunAsync(new AgentRequest("test", AgentName: "OtherAgent"));

        result.Success.Should().BeTrue();
        counting.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task Strategy_Scoped_ShortCircuitsMatchingAgent()
    {
        var llm = new FakeLlmClient([]);
        var tools = new ToolRegistry();
        var strategy = new FilteredStrategy(
            new AlwaysSucceedStrategy(),
            ctx => ctx.AgentName == "FastAgent");
        var runtime = new AgentRuntime(llm, tools, strategies: [strategy]);

        var result = await runtime.RunAsync(new AgentRequest("test", AgentName: "FastAgent"));

        result.Success.Should().BeTrue();
        result.Output.Should().Be("strategy output");
    }

    [Fact]
    public async Task Strategy_Scoped_FallsThroughOnNonMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("llm output")]);
        var tools = new ToolRegistry();
        var strategy = new FilteredStrategy(
            new AlwaysSucceedStrategy(),
            ctx => ctx.AgentName == "FastAgent");
        var runtime = new AgentRuntime(llm, tools, strategies: [strategy]);

        var result = await runtime.RunAsync(new AgentRequest("test", AgentName: "SlowAgent"));

        result.Success.Should().BeTrue();
        result.Output.Should().Be("llm output");
    }

    [Fact]
    public async Task Observer_Scoped_NotifiesMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var spy = new NameCapturingObserver();
        var observer = new FilteredObserver(spy, ctx => ctx.AgentName == "ObservedAgent");
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);

        await runtime.RunAsync(new AgentRequest("test", AgentName: "ObservedAgent"));

        spy.CapturedAgentName.Should().Be("ObservedAgent");
    }

    [Fact]
    public async Task Observer_Scoped_SkipsNonMatchingAgent()
    {
        var llm = new FakeLlmClient([new LlmResponse("done")]);
        var tools = new ToolRegistry();
        var spy = new NameCapturingObserver();
        var observer = new FilteredObserver(spy, ctx => ctx.AgentName == "ObservedAgent");
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);

        await runtime.RunAsync(new AgentRequest("test", AgentName: "IgnoredAgent"));

        spy.CapturedAgentName.Should().BeNull();
    }

    private sealed class AlwaysFailResultValidator : IAgentResultValidator
    {
        public string Name => "AlwaysFail";

        public Task<ValidationResult> ValidateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken ct) =>
            Task.FromResult(new ValidationResult(false, "always fails"));
    }

    private sealed class AlwaysRejectGate : IAgentQualityGate
    {
        public string Name => "AlwaysReject";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<QualityGateResult> EvaluateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken ct) =>
            Task.FromResult(new QualityGateResult(false, false, "always rejected"));
    }

    private sealed class AlwaysSucceedStrategy : IAgentStrategy
    {
        public string Name => "AlwaysSucceed";

        public bool CanExecute(IAgentContext context) => true;

        public Task<StrategyResult> ExecuteAsync(IAgentContext context, CancellationToken ct) =>
            Task.FromResult(new StrategyResult(true, "strategy output"));
    }

    private sealed class BlockAllPolicy : IAgentPolicy
    {
        public string Name => "BlockAll";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<PolicyResult> EvaluateAsync(IAgentContext context, CancellationToken ct) =>
            Task.FromResult(new PolicyResult(true, 0.0, "block", "Blocked."));
    }

    private sealed class CountingMiddleware : IAgentPipelineMiddleware
    {
        public int InvocationCount { get; private set; }

        public string Name => "Counting";

        public async Task<AgentResult> InvokeAsync(IExecutionContext context, AgentPipelineDelegate next)
        {
            InvocationCount++;
            return await next(context);
        }
    }

    // === Test helpers ===

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly Queue<LlmResponse> _responses;

        public FakeLlmClient(IEnumerable<LlmResponse> responses) =>
            _responses = new Queue<LlmResponse>(responses);

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
                return Task.FromResult(
                    new LlmResponse("fallback") { Usage = new LlmTokenUsage(5, 5) });
            return Task.FromResult(_responses.Dequeue() with { Usage = new LlmTokenUsage(5, 5) });
        }
    }

    private sealed class NameCapturingObserver : IAgentObserver
    {
        public string? CapturedAgentName { get; private set; }

        public Task OnGateRejectedAsync(
            IAgentQualityGate gate,
            QualityGateResult result,
            int retryCount,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnLlmCalledAsync(IReadOnlyList<LlmMessage> messages, CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnLlmRespondedAsync(
            LlmResponse response,
            TimeSpan duration,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnPolicyBlockedAsync(
            IAgentPolicy policy,
            PolicyResult result,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnRunCompletedAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnRunStartedAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken ct)
        {
            CapturedAgentName = context.AgentName;
            return Task.CompletedTask;
        }

        public Task OnToolCompletedAsync(
            ITool tool,
            ToolResult result,
            TimeSpan duration,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task OnToolInvokedAsync(
            ITool tool,
            ToolInvocation invocation,
            CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class RejectingInputValidator : IAgentInputValidator
    {
        public string Name => "RejectAll";

        public Task<InputValidationResult> ValidateAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken ct) =>
            Task.FromResult(InputValidationResult.Invalid("always rejected"));
    }

    private sealed class UppercaseTransformer : IAgentResultTransformer
    {
        public string Name => "Uppercase";

        public int Priority => 100;

        public Task<AgentResult> TransformAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken ct) =>
            Task.FromResult(result with { Output = result.Output?.ToUpperInvariant() });
    }
}
