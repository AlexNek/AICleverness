using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public class ExecutionContextTests
{
    [Fact]
    public void Create_ExposesAgentContext()
    {
        var agentContext = CreateAgentContext("my goal");
        var ctx = DefaultExecutionContext.Create(
            CreateRequest("my goal"),
            CreateOptions(),
            agentContext);

        ctx.AgentContext.Should().BeSameAs(agentContext);
    }

    [Fact]
    public void Create_InitializesEmptyArtifacts()
    {
        var ctx = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext());

        ctx.Artifacts.Count.Should().Be(0);
        ctx.Artifacts.Names.Should().BeEmpty();
    }

    [Fact]
    public void Create_InitializesEmptyItems()
    {
        var ctx = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext());

        ctx.Items.Count.Should().Be(0);
        ctx.Items.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Create_InitializesEmptyState()
    {
        var ctx = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext());

        ctx.State.Status.Should().Be(ExecutionStatus.Pending);
        ctx.State.StartedAt.Should().BeNull();
        ctx.State.CompletedAt.Should().BeNull();
        ctx.State.TurnCount.Should().Be(0);
        ctx.State.QualityRetryCount.Should().Be(0);
        ctx.State.ToolRetryCount.Should().Be(0);
        ctx.State.ToolInvocationCount.Should().Be(0);
    }

    [Fact]
    public void Create_SetsMetadata()
    {
        var request = CreateRequest("do something");
        var options = CreateOptions();
        var agentContext = CreateAgentContext("do something");

        var ctx = DefaultExecutionContext.Create(request, options, agentContext);

        ctx.Metadata.Should().NotBeNull();
        ctx.Metadata.ExecutionId.Should().NotBeNullOrWhiteSpace();
        ctx.Metadata.TraceId.Should().NotBeNullOrWhiteSpace();
        ctx.Metadata.Request.Should().BeSameAs(request);
        ctx.Metadata.Options.Should().BeSameAs(options);
        ctx.Metadata.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithAvailableToolNames_IncludesInMetadata()
    {
        var tools = new List<string> { "search", "calculate" };
        var ctx = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext(),
            tools);

        ctx.Metadata.AvailableToolNames.Should().BeEquivalentTo(tools);
    }

    [Fact]
    public void CreateChild_HasOwnState()
    {
        var parent = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext());
        parent.State.MarkStarted();

        var child = parent.CreateChild(
            CreateRequest("child"),
            CreateOptions(),
            CreateAgentContext("child"));

        child.State.Status.Should().Be(ExecutionStatus.Pending);
        child.State.StartedAt.Should().BeNull();
    }

    [Fact]
    public void CreateChild_SharesTraceId()
    {
        var parent = DefaultExecutionContext.Create(
            CreateRequest(),
            CreateOptions(),
            CreateAgentContext());
        var childRequest = CreateRequest("sub-task");
        var childAgentContext = CreateAgentContext("sub-task");

        var child = parent.CreateChild(childRequest, CreateOptions(), childAgentContext);

        child.Metadata.TraceId.Should().Be(parent.Metadata.TraceId);
        child.Metadata.CorrelationId.Should().Be(parent.Metadata.ExecutionId);
        child.Metadata.ExecutionId.Should().NotBe(parent.Metadata.ExecutionId);
    }

    private static DefaultAgentContext CreateAgentContext(string goal = "test") =>
        new() { Goal = goal, State = new AgentState(), Memory = new InMemoryAgentMemory() };

    private static AgentRuntimeOptions CreateOptions() => new();

    private static AgentRequest CreateRequest(string goal = "test") => new(goal);
}
