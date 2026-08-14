using AiCleverness.Models;

using AiClevernessLib.Tests.Testing;

namespace AiClevernessLib.Tests.Runtime;

/// <summary>
/// Tests for the Phase 19 testing infrastructure.
/// </summary>
public sealed class TestingInfrastructureTests
{
    [Fact]
    public void DiffSnapshots_Different_ReturnsDiffs()
    {
        var s1 = new ExecutionSnapshot
                     {
                         ExecutionId = "e1",
                         Goal = "g1",
                         Status = ExecutionStatus.Running,
                         CreatedAt = DateTimeOffset.UtcNow
                     };
        var s2 = new ExecutionSnapshot
                     {
                         ExecutionId = "e2",
                         Goal = "g2",
                         Status = ExecutionStatus.Completed,
                         CreatedAt = DateTimeOffset.UtcNow
                     };

        var diffs = s1.DiffSnapshots(s2);
        Assert.True(diffs.Count >= 3); // ExecutionId, Goal, Status differ
    }

    [Fact]
    public void DiffSnapshots_Identical_ReturnsEmpty()
    {
        var s = new ExecutionSnapshot
                    {
                        ExecutionId = "e1",
                        Goal = "g",
                        Status = ExecutionStatus.Completed,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

        var diffs = s.DiffSnapshots(s);
        Assert.Empty(diffs);
    }

    [Fact]
    public async Task FakeChatClient_DefaultResponse_UsedWhenQueueEmpty()
    {
        var client = new FakeChatClient()
            .SetDefaultResponse("default");

        var r = await client.CompleteAsync([new LlmMessage("user", "hello")]);

        Assert.Equal("default", r.Content);
    }
    // ─── FakeChatClient ──────────────────────────────────────────

    [Fact]
    public async Task FakeChatClient_EnqueueResponse_ReturnsInOrder()
    {
        var client = new FakeChatClient()
            .EnqueueResponse("first")
            .EnqueueResponse("second");

        var r1 = await client.CompleteAsync([new LlmMessage("user", "hello")]);
        var r2 = await client.CompleteAsync([new LlmMessage("user", "hello")]);

        Assert.Equal("first", r1.Content);
        Assert.Equal("second", r2.Content);
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task FakeChatClient_EnqueueToolCallResponse()
    {
        var toolCall = new LlmToolCall("call-1", "my-tool", "{}");
        var client = new FakeChatClient().EnqueueToolCallResponse(toolCall);

        var r = await client.CompleteAsync([new LlmMessage("user", "go")]);

        Assert.Null(r.Content);
        Assert.NotNull(r.ToolCalls);
        Assert.Single(r.ToolCalls);
        Assert.Equal("my-tool", r.ToolCalls[0].Name);
    }

    [Fact]
    public async Task FakeChatClient_NoResponses_Throws()
    {
        var client = new FakeChatClient();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                     client.CompleteAsync([new LlmMessage("user", "hello")]));
        Assert.Contains("no more queued responses", ex.Message);
    }

    [Fact]
    public async Task FakeChatClient_Reset_ClearsEverything()
    {
        var client = new FakeChatClient().EnqueueResponse("x");
        await client.CompleteAsync([new LlmMessage("user", "go")]);
        Assert.Equal(1, client.CallCount);

        client.Reset();
        Assert.Equal(0, client.CallCount);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task FakeChatClient_TracksCalls()
    {
        var client = new FakeChatClient().EnqueueResponse("ok");

        var messages = new[] { new LlmMessage("system", "sys"), new LlmMessage("user", "hello") };
        await client.CompleteAsync(messages);

        Assert.Single(client.Calls);
        Assert.Equal("hello", client.Calls[0].UserMessage);
        Assert.Equal("sys", client.Calls[0].SystemMessage);
        Assert.Equal(2, client.Calls[0].MessageCount);
    }

    // ─── FakeClock ───────────────────────────────────────────────

    [Fact]
    public void FakeClock_Advance()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(start);

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(start.AddMinutes(5), clock.UtcNow);
    }

    [Fact]
    public void FakeClock_AdvanceSeconds()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(start);

        clock.AdvanceSeconds(30);

        Assert.Equal(start.AddSeconds(30), clock.UtcNow);
    }

    [Fact]
    public void FakeClock_SetTo()
    {
        var clock = new FakeClock();
        var target = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        clock.SetTo(target);

        Assert.Equal(target, clock.UtcNow);
    }

    [Fact]
    public async Task FakeMemory_GetKeys()
    {
        var memory = new FakeMemory()
            .WithValue("a", 1)
            .WithValue("b", 2);

        var keys = await memory.GetKeysAsync();

        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public async Task FakeMemory_Reset_ClearsAll()
    {
        var memory = new FakeMemory().WithValue("x", 1);
        await memory.SaveAsync("y", 2);
        memory.Reset();

        Assert.Equal(0, memory.Count);
        Assert.Empty(memory.Operations);
    }

    // ─── FakeMemory ──────────────────────────────────────────────

    [Fact]
    public async Task FakeMemory_SaveAndLoad()
    {
        var memory = new FakeMemory();

        await memory.SaveAsync("key1", "value1");
        var loaded = await memory.LoadAsync<string>("key1");

        Assert.Equal("value1", loaded);
        Assert.Equal(2, memory.Operations.Count); // SAVE + LOAD
    }

    [Fact]
    public async Task FakeMemory_WithValue_Preload()
    {
        var memory = new FakeMemory().WithValue("name", "Alice");

        var loaded = await memory.LoadAsync<string>("name");

        Assert.Equal("Alice", loaded);
        Assert.True(await memory.ContainsAsync("name"));
    }

    [Fact]
    public async Task FakePlanner_Empty_ReturnsEmptyList()
    {
        var planner = FakePlanner.Empty();
        var request = new AgentRequest("goal");
        var context = new FakeAgentContext();

        var steps = await planner.PlanAsync(request, context);

        Assert.Empty(steps);
    }

    // ─── FakePlanner ─────────────────────────────────────────────

    [Fact]
    public async Task FakePlanner_WithSteps_ReturnsSteps()
    {
        var planner = FakePlanner.WithSteps("step A", "step B");

        var request = new AgentRequest("test goal");
        var context = new FakeAgentContext();

        var steps = await planner.PlanAsync(request, context);

        Assert.Equal(2, steps.Count);
        Assert.Equal("step A", steps[0].Description);
        Assert.Equal("step B", steps[1].Description);
        Assert.Equal(1, planner.PlanCallCount);
    }

    [Fact]
    public async Task FakeToolExecutor_DefaultSuccess_WhenQueueEmpty()
    {
        var executor = new FakeToolExecutor().SetDefaultSuccess("default-ok");
        var tool = new SimpleTestTool("t");
        var invocation = new ToolInvocation("t");
        var policy = new ToolExecutionPolicy();

        var r1 = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);
        var r2 = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.Equal(2, executor.ExecutionCount);
    }

    [Fact]
    public async Task FakeToolExecutor_EnqueueFailure()
    {
        var executor = new FakeToolExecutor().EnqueueFailure("oops");
        var tool = new SimpleTestTool("fail-tool");
        var invocation = new ToolInvocation("fail-tool");
        var policy = new ToolExecutionPolicy();

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("oops", result.Error);
    }

    // ─── FakeToolExecutor ────────────────────────────────────────

    [Fact]
    public async Task FakeToolExecutor_EnqueueSuccess_ReturnsResult()
    {
        var executor = new FakeToolExecutor().EnqueueSuccess("done");
        var tool = new SimpleTestTool("test-tool");
        var invocation = new ToolInvocation("test-tool");
        var policy = new ToolExecutionPolicy();

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("done", result.Output);
        Assert.Single(executor.Executions);
        Assert.Equal("test-tool", executor.Executions[0].ToolName);
    }

    [Fact]
    public void ShouldContainOutput_MatchingContent_ReturnsResult()
    {
        var result = new AgentResult(true, "hello world");
        var returned = result.ShouldContainOutput("world");
        Assert.Same(result, returned);
    }

    [Fact]
    public void ShouldContainOutput_NoMatch_Throws()
    {
        var result = new AgentResult(true, "hello");
        Assert.Throws<AgentAssertionException>(() => result.ShouldContainOutput("xyz"));
    }

    [Fact]
    public void ShouldFail_FailedResult_ReturnsResult()
    {
        var result = new AgentResult(false);
        var returned = result.ShouldFail();
        Assert.Same(result, returned);
    }

    [Fact]
    public void ShouldHaveAtLeastSteps_EnoughSteps_ReturnsResult()
    {
        var result = new AgentResult(true, Steps: ["s1", "s2", "s3"]);
        var returned = result.ShouldHaveAtLeastSteps(2);
        Assert.Same(result, returned);
    }

    [Fact]
    public void ShouldHaveAtLeastSteps_NotEnough_Throws()
    {
        var result = new AgentResult(true, Steps: ["s1"]);
        Assert.Throws<AgentAssertionException>(() => result.ShouldHaveAtLeastSteps(3));
    }

    [Fact]
    public void ShouldHaveStepMatching_MatchFound_ReturnsResult()
    {
        var result = new AgentResult(true, Steps: ["step A", "step B"]);
        var returned = result.ShouldHaveStepMatching(s => s.Contains("B"));
        Assert.Same(result, returned);
    }

    [Fact]
    public void ShouldHaveStepMatching_NoMatch_Throws()
    {
        var result = new AgentResult(true, Steps: ["step A"]);
        Assert.Throws<AgentAssertionException>(() => result.ShouldHaveStepMatching(s => s == "Z"));
    }

    [Fact]
    public void ShouldMatchSnapshot_Different_Throws()
    {
        var s1 = new ExecutionSnapshot
                     {
                         ExecutionId = "e1",
                         Goal = "g",
                         Status = ExecutionStatus.Running,
                         CreatedAt = DateTimeOffset.UtcNow
                     };
        var s2 = new ExecutionSnapshot
                     {
                         ExecutionId = "e1",
                         Goal = "g",
                         Status = ExecutionStatus.Completed,
                         CreatedAt = DateTimeOffset.UtcNow
                     };

        Assert.Throws<SnapshotMismatchException>(() => s1.ShouldMatchSnapshot(s2));
    }

    [Fact]
    public void ShouldMatchSnapshot_Matching_DoesNotThrow()
    {
        var s = new ExecutionSnapshot
                    {
                        ExecutionId = "e1",
                        Goal = "g",
                        Status = ExecutionStatus.Completed,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

        s.ShouldMatchSnapshot(s); // Should not throw
    }

    [Fact]
    public void ShouldNotContainOutput_NoMatch_ReturnsResult()
    {
        var result = new AgentResult(true, "hello");
        var returned = result.ShouldNotContainOutput("xyz");
        Assert.Same(result, returned);
    }

    [Fact]
    public void ShouldSucceed_FailedResult_Throws()
    {
        var result = new AgentResult(false, null, "bad");
        Assert.Throws<AgentAssertionException>(() => result.ShouldSucceed());
    }

    // ─── ExecutionAssertions ─────────────────────────────────────

    [Fact]
    public void ShouldSucceed_SuccessfulResult_ReturnsResult()
    {
        var result = new AgentResult(true, "output");
        var returned = result.ShouldSucceed();
        Assert.Same(result, returned);
    }

    [Fact]
    public void SnapshotBuilder_BuildsExpectedSnapshot()
    {
        var snapshot = SnapshotTesting.CreateSnapshot("exec-42", "my goal")
            .WithStatus(ExecutionStatus.Completed)
            .WithResult(true, "output!")
            .WithTurnCount(5)
            .WithToolInvocations(3)
            .WithQualityRetries(1)
            .WithToolRetries(0)
            .WithTools("tool-a", "tool-b")
            .Build();

        Assert.Equal("exec-42", snapshot.ExecutionId);
        Assert.Equal("my goal", snapshot.Goal);
        Assert.Equal(ExecutionStatus.Completed, snapshot.Status);
        Assert.True(snapshot.ResultSuccess);
        Assert.Equal("output!", snapshot.ResultOutput);
        Assert.Equal(5, snapshot.TurnCount);
        Assert.Equal(3, snapshot.ToolInvocationCount);
        Assert.Equal(1, snapshot.QualityRetryCount);
        Assert.Equal(0, snapshot.ToolRetryCount);
        Assert.Equal(2, snapshot.AvailableToolNames.Count);
    }

    // ─── SnapshotTesting ─────────────────────────────────────────

    [Fact]
    public void SnapshotRoundTrip()
    {
        var snapshot = new ExecutionSnapshot
                           {
                               ExecutionId = "exec-1",
                               Goal = "test goal",
                               Status = ExecutionStatus.Completed,
                               ResultSuccess = true,
                               ResultOutput = "done",
                               TurnCount = 3,
                               ToolInvocationCount = 2,
                               CreatedAt = new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero)
                           };

        var json = snapshot.ToSnapshotJson();
        var restored = SnapshotTesting.FromSnapshotJson(json);

        Assert.NotNull(restored);
        Assert.Equal("exec-1", restored.ExecutionId);
        Assert.Equal("test goal", restored.Goal);
        Assert.Equal(ExecutionStatus.Completed, restored.Status);
        Assert.True(restored.ResultSuccess);
        Assert.Equal("done", restored.ResultOutput);
    }

    private sealed class FakeAgentContext : AiCleverness.Abstractions.IAgentContext
    {
        private readonly Dictionary<string, object> _properties = new();

        public string AgentName => "default";

        public string Goal => "test goal";

        public AiCleverness.Abstractions.IAgentMemory Memory { get; } = new FakeMemory();

        public IReadOnlyDictionary<string, object> Properties => _properties;

        public AgentState State { get; } = new();

        public T? GetProperty<T>(string key) =>
            _properties.TryGetValue(key, out var v) && v is T typed ? typed : default;

        public void SetProperty<T>(string key, T value) => _properties[key] = value!;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private sealed class SimpleTestTool : AiCleverness.Abstractions.ITool
    {
        public ToolDefinition Definition => new(Name, Description);

        public string Description => "test";

        public string Name { get; }

        public SimpleTestTool(string name)
        {
            Name = name;
        }

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }
}
