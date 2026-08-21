using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class IdempotencyTests
{
    public sealed class IdempotentToolExecutorTests
    {
        [Fact]
        public async Task DifferentArgs_ExecutesBothTimes()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("search");

            await executor.ExecuteAsync(
                tool,
                new ToolInvocation("search", new Dictionary<string, object> { ["q"] = "AI" }),
                new ToolExecutionPolicy(),
                default);
            await executor.ExecuteAsync(
                tool,
                new ToolInvocation("search", new Dictionary<string, object> { ["q"] = "ML" }),
                new ToolExecutionPolicy(),
                default);

            inner.CallCount.Should().Be(2);
        }

        [Fact]
        public async Task DifferentExecutionScope_NoCacheSharing()
        {
            var cache = new InMemoryIdempotencyCache();
            var inner = new CountingToolExecutor();
            var executor1 = new IdempotentToolExecutor(inner, cache, "exec-1");
            var executor2 = new IdempotentToolExecutor(inner, cache, "exec-2");
            var tool = new FakeTool("search");
            var invocation = new ToolInvocation(
                "search",
                new Dictionary<string, object> { ["q"] = "AI" });

            await executor1.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);
            await executor2.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);

            inner.CallCount.Should().Be(2); // Different scope, no sharing
        }

        [Fact]
        public async Task ExplicitInvocationId_UsedAsKey()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("action");

            var inv1 = new ToolInvocation(
                "action",
                new Dictionary<string, object> { ["x"] = "1" },
                InvocationId: "id-abc");
            var inv2 = new ToolInvocation(
                "action",
                new Dictionary<string, object> { ["x"] = "different" },
                InvocationId: "id-abc");

            await executor.ExecuteAsync(tool, inv1, new ToolExecutionPolicy(), default);
            await executor.ExecuteAsync(tool, inv2, new ToolExecutionPolicy(), default);

            // Same InvocationId → cached, even though args differ
            inner.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task FailedResult_IsNotCached()
        {
            var inner = new FailingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("fail");
            var invocation = new ToolInvocation("fail");

            await executor.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);
            await executor.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);

            inner.CallCount.Should().Be(2); // Retried because not cached
            cache.Count.Should().Be(0);
        }

        [Fact]
        public async Task FirstCall_Executes_And_CachesResult()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("search");
            var invocation = new ToolInvocation(
                "search",
                new Dictionary<string, object> { ["q"] = "AI" });

            var result = await executor.ExecuteAsync(
                             tool,
                             invocation,
                             new ToolExecutionPolicy(),
                             default);

            result.Success.Should().BeTrue();
            inner.CallCount.Should().Be(1);
            cache.Count.Should().Be(1);
        }

        [Fact]
        public async Task TryGetCachedResult_AfterSuccessfulExecution_ReturnsCachedResult()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("search");
            var invocation = new ToolInvocation(
                "search",
                new Dictionary<string, object> { ["q"] = "AI" });

            await executor.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);

            var hit = executor.TryGetCachedResult(tool, invocation, out var cached);

            hit.Should().BeTrue();
            cached!.Output.Should().Be("result");
            inner.CallCount.Should().Be(1);
        }

        [Fact]
        public void TryGetCachedResult_WhenMissing_ReturnsFalse()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("search");
            var invocation = new ToolInvocation(
                "search",
                new Dictionary<string, object> { ["q"] = "AI" });

            var hit = executor.TryGetCachedResult(tool, invocation, out var cached);

            hit.Should().BeFalse();
            cached.Should().BeNull();
            inner.CallCount.Should().Be(0);
        }

        [Fact]
        public async Task SecondCall_SameArgs_ReturnsCached()
        {
            var inner = new CountingToolExecutor();
            var cache = new InMemoryIdempotencyCache();
            var executor = new IdempotentToolExecutor(inner, cache, "exec-1");
            var tool = new FakeTool("search");
            var invocation = new ToolInvocation(
                "search",
                new Dictionary<string, object> { ["q"] = "AI" });

            await executor.ExecuteAsync(tool, invocation, new ToolExecutionPolicy(), default);
            var result2 = await executor.ExecuteAsync(
                              tool,
                              invocation,
                              new ToolExecutionPolicy(),
                              default);

            result2.Success.Should().BeTrue();
            inner.CallCount.Should().Be(1); // NOT called twice
        }
    }

    public sealed class InMemoryIdempotencyCacheTests
    {
        [Fact]
        public void Clear_RemovesMatchingScope()
        {
            var cache = new InMemoryIdempotencyCache();
            cache.Set("exec-1:tool:abc", new ToolResult(true, "1"));
            cache.Set("exec-1:tool:def", new ToolResult(true, "2"));
            cache.Set("exec-2:tool:abc", new ToolResult(true, "3"));

            cache.Clear("exec-1");

            cache.Count.Should().Be(1);
            cache.TryGet("exec-2:tool:abc", out _).Should().BeTrue();
        }

        [Fact]
        public void Set_And_TryGet_ReturnsTrue()
        {
            var cache = new InMemoryIdempotencyCache();
            var result = new ToolResult(true, "ok");

            cache.Set("key1", result);

            cache.TryGet("key1", out var cached).Should().BeTrue();
            cached!.Output.Should().Be("ok");
        }

        [Fact]
        public void Set_DuplicateKey_DoesNotOverwrite()
        {
            var cache = new InMemoryIdempotencyCache();
            cache.Set("key", new ToolResult(true, "first"));
            cache.Set("key", new ToolResult(true, "second"));

            cache.TryGet("key", out var cached).Should().BeTrue();
            cached!.Output.Should().Be("first"); // TryAdd semantics
        }

        [Fact]
        public void TryGet_MissingKey_ReturnsFalse()
        {
            var cache = new InMemoryIdempotencyCache();

            cache.TryGet("missing", out _).Should().BeFalse();
        }
    }

    // === Test helpers ===

    private sealed class CountingToolExecutor : IToolExecutor
    {
        public int CallCount { get; private set; }

        public Task<ToolResult> ExecuteAsync(
            ITool tool,
            ToolInvocation invocation,
            ToolExecutionPolicy policy,
            CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new ToolResult(true, "result"));
        }
    }

    private sealed class FailingToolExecutor : IToolExecutor
    {
        public int CallCount { get; private set; }

        public Task<ToolResult> ExecuteAsync(
            ITool tool,
            ToolInvocation invocation,
            ToolExecutionPolicy policy,
            CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new ToolResult(false, null, "failed"));
        }
    }

    private sealed class FakeTool : ITool
    {
        public ToolDefinition Definition => new(Name, Description);

        public string Description => Name;

        public string Name { get; }

        public FakeTool(string name) => Name = name;

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }
}
