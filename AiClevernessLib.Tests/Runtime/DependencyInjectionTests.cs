using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.DecisionTree;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAiClevernessRuntime_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<StubLlmClient>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IToolRegistry>().Should().NotBeNull();
        provider.GetService<IToolExecutor>().Should().NotBeNull();
        provider.GetService<IAgentMemory>().Should().NotBeNull();
        provider.GetService<IAgentRuntime>().Should().NotBeNull();
        provider.GetService<ILlmCompletionPipeline>().Should().NotBeNull();
        provider.GetService<IPlannerRegistry>().Should().NotBeNull();
        provider.GetService<IStrategyRegistry>().Should().NotBeNull();
        provider.GetService<AgentRuntimeOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddAiClevernessRuntime_RegistersCoreServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<StubLlmClient>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAgentRuntime>()
            .Should().BeSameAs(provider.GetRequiredService<IAgentRuntime>());
        provider.GetRequiredService<IToolRegistry>()
            .Should().BeSameAs(provider.GetRequiredService<IToolRegistry>());
    }

    [Fact]
    public void AddAiClevernessRuntime_AppliesOptionsConfiguration()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime(options => options.DefaultMaxTurns = 7);
        services.AddAiClevernessLlmClient<StubLlmClient>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<AgentRuntimeOptions>().DefaultMaxTurns.Should().Be(7);
    }

    [Fact]
    public void AddAiClevernessLlmClient_DoesNotReplaceExistingRegistration()
    {
        var existing = new StubLlmClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILlmClient>(existing);

        services.AddAiClevernessLlmClient<StubLlmClient>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILlmClient>().Should().BeSameAs(existing);
    }

    [Fact]
    public void AddAgentTool_ResolvesSameInstanceAsConcreteAndInterface()
    {
        var services = new ServiceCollection();
        services.AddAgentTool<StubTool>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<StubTool>()
            .Should().BeSameAs(provider.GetRequiredService<ITool>());
    }

    [Fact]
    public void AddDecisionTreeExecution_RegistersSharedPipelineAndConversationFactory()
    {
        var services = new ServiceCollection();
        services.AddDecisionTreeExecution();
        services.AddAiClevernessLlmClient<StubLlmClient>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILlmCompletionPipeline>().Should().BeOfType<DefaultLlmCompletionPipeline>();
        provider.GetRequiredService<IConversationManagerFactory>().Should().NotBeNull();
        provider.GetRequiredService<DecisionTreeExecutor>().Should().NotBeNull();
    }
    [Fact]
    public void AddAiClevernessRuntime_NullServices_Throws()
    {
        var act = () => ((IServiceCollection)null!).AddAiClevernessRuntime();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAiClevernessLlmClient_NullServices_Throws()
    {
        var act = () => ((IServiceCollection)null!).AddAiClevernessLlmClient<StubLlmClient>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAgentTool_NullServices_Throws()
    {
        var act = () => ((IServiceCollection)null!).AddAgentTool<StubTool>();

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class StubLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmResponse("stub"));
    }

    private sealed class StubTool : ITool
    {
        public ToolDefinition Definition { get; } = new("stub-tool", "A stub tool.");

        public string Description => "A stub tool.";

        public string Name => "stub-tool";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(true, "stub"));
    }
}
