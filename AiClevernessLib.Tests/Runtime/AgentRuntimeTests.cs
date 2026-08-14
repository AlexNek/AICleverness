using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class AgentRuntimeTests
{
    [Fact]
    public async Task RunAsync_UsesRuntimeOptionsAsDefaults()
    {
        var llm = new FakeLlmClient([new LlmResponse(null)]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions { DefaultMaxTurns = 1 });
        var request = new AgentRequest("No answer");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeFalse();
        result.Reasoning.Should().Be("Exhausted 1 turns without a final response.");
    }

    [Fact]
    public async Task RunAsync_WhenQualityGateRequestsRetry_RerunsToolLoop()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse("bad answer"),
                new LlmResponse("good answer")
            ]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            qualityGates: [new RequiresGoodAnswerGate()]);
        var request = new AgentRequest(
            "Answer well",
            Parameters:
            new Dictionary<string, object> { [AgentPropertyKeys.MaxQualityRetries] = 1 });

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("good answer");
        result.Steps.Should()
            .Contain(s => s.Contains("Quality gate RequiresGoodAnswer rejected result"));
        result.Steps.Should().Contain(s => s.Contains("Retrying after quality feedback"));
    }

    [Fact]
    public async Task RunAsync_WithNoToolCalls_ReturnsContentDirectly()
    {
        var llm = new FakeLlmClient([new LlmResponse("Direct answer")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Hello");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Direct answer");
    }

    [Fact]
    public async Task RunAsync_WithObserver_NotifiesGateRejected()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse("bad"),
                new LlmResponse("good answer")
            ]);
        var tools = new ToolRegistry();
        var observer = new SpyObserver();
        var runtime = new AgentRuntime(
            llm,
            tools,
            qualityGates: [new RequiresGoodAnswerGate()],
            observers: [observer]);
        var request = new AgentRequest(
            "Test",
            Parameters:
            new Dictionary<string, object> { [AgentPropertyKeys.MaxQualityRetries] = 1 });

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("good answer");
        observer.GateRejectedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithObserver_NotifiesPolicyBlocked()
    {
        var llm = new FakeLlmClient([new LlmResponse("should not run")]);
        var tools = new ToolRegistry();
        var observer = new SpyObserver();
        var policy = new BlockAllPolicy();
        var runtime = new AgentRuntime(llm, tools, policies: [policy], observers: [observer]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeFalse();
        result.Reasoning.Should().Be("BlockAllPolicy blocked everything.");
        observer.PolicyBlockedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithObserver_NotifiesRunLifecycle()
    {
        var llm = new FakeLlmClient([new LlmResponse("final answer")]);
        var tools = new ToolRegistry();
        var observer = new SpyObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        observer.RunStartedCalled.Should().BeTrue();
        observer.RunCompletedCalled.Should().BeTrue();
        observer.LlmCalled.Should().BeTrue();
        observer.LlmResponded.Should().BeTrue();
        result.Output.Should().Be("final answer");
    }

    [Fact]
    public async Task RunAsync_WithObserver_NotifiesToolEvents()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                        [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var observer = new SpyObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);
        var request = new AgentRequest(
            "Test",
                ["echo"],
            new Dictionary<string, object> { [AgentPropertyKeys.MaxTurns] = 3 });

        await runtime.RunAsync(request);

        observer.ToolInvoked.Should().BeTrue();
        observer.ToolCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithQualityGateReplacementResult_UsesReplacement()
    {
        var llm = new FakeLlmClient([new LlmResponse("original output")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            qualityGates: [new ReplacementQualityGate()]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("replaced output");
        result.Reasoning.Should().Be("Gate replaced the result.");
    }

    [Fact]
    public async Task RunAsync_WithToolCall_ReturnsToolResultAndFinalContent()
    {
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                        [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done: hello")
            ]);

        var tools = new ToolRegistry();
        tools.Register(new EchoTool());

        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest(
            "Say hello using the echo tool",
                ["echo"],
            new Dictionary<string, object> { [AgentPropertyKeys.MaxTurns] = 3 });

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Done: hello");
        result.Steps.Should().Contain(s => s.Contains("Calling tool echo"));
        result.Steps.Should().Contain(s => s.Contains("echo succeeded"));
        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(20);
        result.Usage.CompletionTokens.Should().Be(10);
    }

    [Fact]
    public async Task RunAsync_WithTransformer_ReturnsTransformedResult()
    {
        var llm = new FakeLlmClient([new LlmResponse("trim me   ")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            transformers: [new TrimOutputTransformer()]);
        var request = new AgentRequest("Return text");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("trim me");
    }

    [Fact]
    public async Task RunAsync_WithValidatorFailure_MarksResultFailed()
    {
        var llm = new FakeLlmClient([new LlmResponse("bad output")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            validators: [new FailingValidator()]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeFalse();
        result.Reasoning.Should().Be("Validation: output must contain valid");
        result.Steps.Should().Contain(s => s.Contains("Validator FailingValidator failed"));
    }

    [Fact]
    public async Task RunAsync_WithValidatorPassing_LeavesResultSuccess()
    {
        var llm = new FakeLlmClient([new LlmResponse("valid output")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            validators: [new PassingValidator()]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("valid output");
    }

    [Fact]
    public async Task RunAsync_OnCancellation_ThrowsOperationCanceledException()
    {
        var llm = new FakeLlmClient([new LlmResponse("never")]);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("cancelled");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => runtime.RunAsync(request, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_OnLlmTimeout_ReturnsFailedResult()
    {
        var llm = new TimeoutLlmClient();
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions { DefaultCompletionTimeoutSeconds = 1 });
        var request = new AgentRequest("timeout test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeFalse();
        result.Reasoning.Should().Contain("timed out");
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

    private sealed class EchoTool : ITool
    {
        public ToolDefinition Definition =>
            new(
                Name,
                Description,
                """
                {
                    "type": "object",
                    "properties": {
                        "message": { "type": "string" }
                    },
                    "required": ["message"]
                }
                """);

        public string Description => "Echoes a message.";

        public string Name => "echo";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            var message = invocation.Arguments.TryGetValue("message", out var m)
                              ? m?.ToString()
                              : null;
            return Task.FromResult(new ToolResult(true, message ?? "(empty)"));
        }
    }

    private sealed class FailingValidator : IAgentResultValidator
    {
        public string Name => "FailingValidator";

        public Task<ValidationResult> ValidateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            var valid = result.Output?.Contains("valid", StringComparison.OrdinalIgnoreCase)
                        == true;
            return Task.FromResult(
                new ValidationResult(
                    valid,
                    valid ? null : "Validation: output must contain valid"));
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

    private sealed class PassingValidator : IAgentResultValidator
    {
        public string Name => "PassingValidator";

        public Task<ValidationResult> ValidateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ValidationResult(true));
        }
    }

    private sealed class ReplacementQualityGate : IAgentQualityGate
    {
        public string Name => "ReplacementGate";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context) => true;

        public Task<QualityGateResult> EvaluateAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            var replacement = result with
                                  {
                                      Output = "replaced output",
                                      Reasoning = "Gate replaced the result."
                                  };
            return Task.FromResult(new QualityGateResult(true, false, null, replacement));
        }
    }

    private sealed class RequiresGoodAnswerGate : IAgentQualityGate
    {
        public string Name => "RequiresGoodAnswer";

        public int Priority => 100;

        public bool AppliesTo(IAgentContext context)
        {
            return true;
        }

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

    private sealed class SpyObserver : IAgentObserver
    {
        public bool GateRejectedCalled { get; private set; }

        public bool LlmCalled { get; private set; }

        public bool LlmResponded { get; private set; }

        public bool PolicyBlockedCalled { get; private set; }

        public bool RunCompletedCalled { get; private set; }

        public bool RunStartedCalled { get; private set; }

        public bool ToolCompleted { get; private set; }

        public bool ToolInvoked { get; private set; }

        public Task OnGateRejectedAsync(
            IAgentQualityGate gate,
            QualityGateResult result,
            int retryCount,
            CancellationToken cancellationToken)
        {
            GateRejectedCalled = true;
            return Task.CompletedTask;
        }

        public Task OnLlmCalledAsync(
            IReadOnlyList<LlmMessage> messages,
            CancellationToken cancellationToken)
        {
            LlmCalled = true;
            return Task.CompletedTask;
        }

        public Task OnLlmRespondedAsync(
            LlmResponse response,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            LlmResponded = true;
            return Task.CompletedTask;
        }

        public Task OnPolicyBlockedAsync(
            IAgentPolicy policy,
            PolicyResult result,
            CancellationToken cancellationToken)
        {
            PolicyBlockedCalled = true;
            return Task.CompletedTask;
        }

        public Task OnRunCompletedAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            RunCompletedCalled = true;
            return Task.CompletedTask;
        }

        public Task OnRunStartedAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            RunStartedCalled = true;
            return Task.CompletedTask;
        }

        public Task OnToolCompletedAsync(
            ITool tool,
            ToolResult result,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            ToolCompleted = true;
            return Task.CompletedTask;
        }

        public Task OnToolInvokedAsync(
            ITool tool,
            ToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            ToolInvoked = true;
            return Task.CompletedTask;
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

    private sealed class TrimOutputTransformer : IAgentResultTransformer
    {
        public string Name => "TrimOutput";

        public int Priority => 100;

        public Task<AgentResult> TransformAsync(
            AgentResult result,
            IAgentContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result with { Output = result.Output?.Trim() });
        }
    }
}
