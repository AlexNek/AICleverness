using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using AiClevernessLib.Tests.Testing;

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
    public async Task RunAsync_WithEmptyAllowedToolNames_OffersNoTools()
    {
        // Arrange — an explicit empty list means "no tools"; only null is unrestricted
        var llm = new FakeChatClient().SetDefaultResponse("final answer");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Test", AllowedToolNames: []);

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the registered tool must not be offered to the LLM
        result.Success.Should().BeTrue();
        llm.Calls.Should().NotBeEmpty();
        llm.Calls.Should().OnlyContain(c => c.Tools == null || c.Tools.Count == 0);
    }

    [Fact]
    public async Task RunAsync_WithoutAllowedToolNames_OffersAllRegisteredTools()
    {
        // Arrange — null (the default) means unrestricted
        var llm = new FakeChatClient().SetDefaultResponse("final answer");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Test");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — every registered tool is offered to the LLM
        result.Success.Should().BeTrue();
        llm.Calls.Should().OnlyContain(c => c.Tools != null && c.Tools.Count == 1);
    }

    [Fact]
    public async Task RunAsync_WithEmptyAllowedToolNames_NeverExecutesToolCall()
    {
        // Arrange — even if the LLM names a tool, an empty list must block execution
        var llm = new FakeChatClient()
            .EnqueueToolCallResponse(new LlmToolCall("call-1", "echo", "{\"message\":\"hi\"}"))
            .SetDefaultResponse("final answer");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var observer = new SpyObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);
        var request = new AgentRequest("Test", AllowedToolNames: []);

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the excluded tool never runs; the model is told it is not allowed
        result.Success.Should().BeTrue();
        result.Output.Should().Be("final answer");
        observer.ToolInvoked.Should().BeFalse();
        result.Steps.Should().Contain(s => s.Contains("Tool 'echo' is not allowed for this run."));
    }

    [Fact]
    public async Task RunAsync_WithExplicitAllowedToolNames_NeverExecutesExcludedTool()
    {
        // Arrange — the model names a registered tool that is not in the allowed list
        var llm = new FakeChatClient()
            .EnqueueToolCallResponse(new LlmToolCall("call-1", "echo", "{\"message\":\"hi\"}"))
            .SetDefaultResponse("final answer");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var observer = new SpyObserver();
        var runtime = new AgentRuntime(llm, tools, observers: [observer]);
        var request = new AgentRequest("Test", AllowedToolNames: ["some_other_tool"]);

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the excluded tool never runs
        result.Success.Should().BeTrue();
        result.Output.Should().Be("final answer");
        observer.ToolInvoked.Should().BeFalse();
        result.Steps.Should().Contain(s => s.Contains("Tool 'echo' is not allowed for this run."));
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
        result.FailureKind.Should().Be(EFailureKind.LlmTimeout);
    }

    [Fact]
    public async Task RunAsync_WithToolCallAndReasoningText_ReportsReasoningInSteps()
    {
        // Arrange — the model returns reasoning text alongside a tool call
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    "Let me check the pricing page directly",
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);

        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Test reasoning visibility");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the reasoning text is reported before the tool call
        result.Success.Should().BeTrue();
        result.Steps.Should().Contain(s => s.Contains("Let me check the pricing page directly"));
        result.Steps.Should().Contain(s => s.Contains("Calling tool echo"));

        // Verify ordering: reasoning must appear before the tool call
        var reasoningIndex = result.Steps.Select((s, i) => (s, i)).First(x => x.s.Contains("Let me check the pricing page directly")).i;
        var toolCallIndex = result.Steps.Select((s, i) => (s, i)).First(x => x.s.Contains("Calling tool echo")).i;
        reasoningIndex.Should().BeGreaterOrEqualTo(0);
        toolCallIndex.Should().BeGreaterOrEqualTo(0);
        reasoningIndex.Should().BeLessThan(toolCallIndex);
    }

    [Fact]
    public async Task RunAsync_WithToolCallAndLongReasoningText_TruncatesToMax()
    {
        // Arrange — the model returns very long reasoning text (> 500 chars)
        var longReasoning = new string('x', 600);
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    longReasoning,
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);

        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Test truncation");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — the reasoning text is truncated with "..." suffix
        result.Success.Should().BeTrue();
        var reasoningStep = result.Steps.FirstOrDefault(s => s.Contains("xxxx"));
        reasoningStep.Should().NotBeNull();

        // Exact truncation contract: "  " (2 spaces) + 500 chars + "..." = 505 total
        reasoningStep!.Length.Should().Be(505, "truncation limit is 500 chars plus 2-space prefix and '...' suffix");
        reasoningStep.Should().EndWith("...");
        reasoningStep.Should().StartWith("  ");
        // Verify the content is exactly 500 'x' characters before the suffix
        reasoningStep.Substring(2, 500).Should().Be(new string('x', 500));
    }

    [Fact]
    public async Task RunAsync_WithToolCallAndNoReasoningText_DoesNotReportEmptyReasoning()
    {
        // Arrange — the model returns only tool calls, no reasoning text
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);

        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest("Test no reasoning");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — no empty reasoning step is added
        result.Success.Should().BeTrue();
        result.Steps.Should().Contain(s => s.Contains("Calling tool echo"));
        // All steps should have meaningful content (no empty or whitespace-only steps)
        result.Steps.Should().NotContain(s => string.IsNullOrWhiteSpace(s));
    }

    [Fact]
    public async Task RunAsync_WithToolResultOutput_ReportsFirstLineSummary()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"first line\\nsecond line\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var progress = new RecordingProgress();
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest("Test result summary"),
            progress);

        // Assert
        result.Steps.Should().Contain("  echo succeeded: first line");
        progress.Messages.Should().Contain("  echo succeeded: first line");
    }

    [Fact]
    public async Task RunAsync_WithLongToolResult_LimitsSummaryTo100Characters()
    {
        // Arrange
        var longMessage = new string('x', 150);
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                    [new LlmToolCall(
                        "call-1",
                        "echo",
                        $"{{\"message\":\"{longMessage}\"}}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test result truncation"));

        // Assert
        var summary = result.Steps.Single(s => s.StartsWith("  echo succeeded: ", StringComparison.Ordinal));
        var preview = summary["  echo succeeded: ".Length..];
        preview.Length.Should().Be(100);
        preview.Should().EndWith("...");
        preview[..^3].Should().Be(new string('x', 97));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_WithEmptyToolResult_DoesNotAppendSummary(string output)
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                    [new LlmToolCall("call-1", "echo", $"{{\"message\":\"{output}\"}}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test empty result"));

        // Assert
        result.Steps.Should().Contain("  echo succeeded");
        result.Steps.Should().NotContain(s => s.StartsWith("  echo succeeded: ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithFailedToolResult_PreservesFailureFormat()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(null, [new LlmToolCall("call-1", "fail", "{}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new FailingTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test failed result"));

        // Assert
        result.Steps.Should().Contain("  fail failed: expected failure");
    }

    [Fact]
    public async Task RunAsync_WithToolCall_ShowsModelAndPreferredKeyArgument()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    null,
                    [new LlmToolCall(
                        "call-1",
                        "echo",
                        "{\"query\":\"search\",\"url\":\"https://test.example.com/item\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);
        var request = new AgentRequest(
            "Test decision metadata",
            Parameters: new Dictionary<string, object> { [AgentPropertyKeys.Model] = "test-model" });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Steps.Should()
            .Contain("  [test-model] Decision: echo — \"https://test.example.com/item\"");
    }

    [Fact]
    public async Task RunAsync_WithToolCallAndNoModel_UsesLlmDecisionLabel()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(null, [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test model fallback"));

        // Assert
        result.Steps.Should().Contain("  [LLM] Decision: echo — \"hello\"");
    }

    [Fact]
    public async Task RunAsync_WithJsonReasoning_ReportsReasoningField()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    "{\"reasoning\":\"The first result looks relevant.\",\"extra\":\"ignored\"}",
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test JSON reasoning"));

        // Assert
        result.Steps.Should().Contain("  The first result looks relevant.");
        result.Steps.Should().NotContain(s => s.Contains("\"extra\":\"ignored\""));
    }

    [Fact]
    public async Task RunAsync_WithMalformedJsonContent_ContinuesToolLoop()
    {
        // Arrange
        const string malformedContent = "{not-json";
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    malformedContent,
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test malformed content"));

        // Assert
        result.Success.Should().BeTrue();
        result.Steps.Should().Contain("  {not-json");
        result.Steps.Should().Contain(s => s.Contains("Decision: echo"));
    }

    [Fact]
    public async Task RunAsync_WithToolCall_ReportsDecisionBeforeInvocation()
    {
        // Arrange
        var llm = new FakeLlmClient(
            [
                new LlmResponse(
                    "I need to check this value",
                    [new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}")]),
                new LlmResponse("Done")
            ]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(llm, tools);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test ordering"));

        // Assert
        var steps = result.Steps.ToList();
        var reasoningIndex = steps.IndexOf("  I need to check this value");
        var decisionIndex = steps.IndexOf("  [LLM] Decision: echo — \"hello\"");
        var invocationIndex = steps.IndexOf("Calling tool echo({\"message\":\"hello\"})");
        reasoningIndex.Should().BeLessThan(decisionIndex);
        decisionIndex.Should().BeLessThan(invocationIndex);
    }

    [Fact]
    public async Task RunAsync_WithCachedToolResult_SuppressesRealInvocationReporting()
    {
        // Arrange
        var llm = new FakeChatClient()
            .EnqueueToolCallResponse(new LlmToolCall("call-1", "echo", "{\"message\":\"hello\"}"))
            .EnqueueResponse("Done");
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var observer = new SpyObserver();
        var executor = new CacheHitToolExecutor();
        var runtime = new AgentRuntime(
            llm,
            tools,
            toolExecutor: executor,
            observers: [observer]);

        // Act
        var result = await runtime.RunAsync(new AgentRequest("Test cached tool result"));

        // Assert
        result.Success.Should().BeTrue();
        result.Steps.Should().Contain("  [LLM] Decision: echo — \"hello\"");
        result.Steps.Should().Contain("  echo reused cached result: cached output");
        result.Steps.Should().NotContain(s => s.StartsWith("Calling tool echo", StringComparison.Ordinal));
        executor.ExecuteCalled.Should().BeFalse();
        observer.ToolInvoked.Should().BeFalse();
        observer.ToolCompleted.Should().BeFalse();

        llm.Calls.Should().HaveCount(2);
        llm.Calls[1].Messages.Should()
            .Contain(message => message.Role == "tool" && message.Content == "cached output");
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

    private sealed class RecordingProgress : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value) => Messages.Add(value);
    }

    private sealed class FailingTool : ITool
    {
        public ToolDefinition Definition => new(Name, Description);

        public string Description => "Always returns a failure.";

        public string Name => "fail";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(false, null, "expected failure"));
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
