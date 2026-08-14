using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DefaultToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRetriesExhausted_ReturnsFailure()
    {
        var executor = new DefaultToolExecutor();
        Func<Task<ToolResult>> alwaysFails =
            () => throw new InvalidOperationException("always fails");
        var tool = new FakeTool("failing-tool", alwaysFails);

        var invocation = new ToolInvocation("failing-tool", new Dictionary<string, object>());
        var policy = new ToolExecutionPolicy(MaxRetries: 1);

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Exception:");
        result.Error.Should().Contain("always fails");
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolSucceeds_ReturnsSuccessResult()
    {
        var executor = new DefaultToolExecutor();
        var tool = new FakeTool("test-tool", () => Task.FromResult(new ToolResult(true, "ok")));
        var invocation = new ToolInvocation("test-tool", new Dictionary<string, object>());
        var policy = new ToolExecutionPolicy();

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("ok");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_Throws()
    {
        var executor = new DefaultToolExecutor();
        using var cts = new CancellationTokenSource();
        var tool = new FakeTool(
            "cancel-tool",
            async ct =>
                {
                    cts.Cancel();
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    return new ToolResult(true, "should not complete");
                });
        var invocation = new ToolInvocation("cancel-tool", new Dictionary<string, object>());
        var policy = new ToolExecutionPolicy();

        var act = () => executor.ExecuteAsync(tool, invocation, policy, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithRetry_RetriesOnException()
    {
        var attempt = 0;
        var executor = new DefaultToolExecutor();
        var tool = new FakeTool(
            "retry-tool",
            () =>
                {
                    attempt++;
                    if (attempt < 2)
                        throw new InvalidOperationException("transient");
                    return Task.FromResult(new ToolResult(true, "recovered"));
                });
        var invocation = new ToolInvocation("retry-tool", new Dictionary<string, object>());
        var policy = new ToolExecutionPolicy(MaxRetries: 2);

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("recovered");
        attempt.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ReturnsTimeoutFailure()
    {
        var executor = new DefaultToolExecutor();
        var tool = new FakeTool(
            "slow-tool",
            async ct =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    return new ToolResult(true, "too late");
                });
        var invocation = new ToolInvocation("slow-tool", new Dictionary<string, object>());
        var policy = new ToolExecutionPolicy(Timeout: TimeSpan.FromMilliseconds(1));

        var result = await executor.ExecuteAsync(tool, invocation, policy, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
    }

    private sealed class FakeTool : ITool
    {
        private readonly Func<CancellationToken, Task<ToolResult>> _execute;

        public ToolDefinition Definition => new(Name, Description);

        public string Description => $"Fake tool: {Name}";

        public string Name { get; }

        public FakeTool(string name, Func<CancellationToken, Task<ToolResult>> execute)
        {
            Name = name;
            _execute = execute;
        }

        public FakeTool(string name, Func<Task<ToolResult>> executeAsync)
        {
            Name = name;
            _execute = _ => executeAsync();
        }

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _execute(cancellationToken);
        }
    }
}
