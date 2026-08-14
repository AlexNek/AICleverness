using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class PipelineTests
{
    [Fact]
    public async Task AgentRuntime_WithCustomMiddleware_ExecutesDuringPipeline()
    {
        var llm = new FakeLlmClient([new LlmResponse("raw result")]);
        var tools = new ToolRegistry();
        var middleware = new UppercaseMiddleware();

        var runtime = new AgentRuntime(llm, tools, middleware: [middleware]);
        var request = new AgentRequest("Test");

        var result = await runtime.RunAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("RAW RESULT");
    }

    [Fact]
    public async Task Pipeline_EmptyMiddleware_RunsTerminalDirectly()
    {
        var builder = new AgentPipelineBuilder();
        builder.UseTerminal(_ => Task.FromResult(new AgentResult(true, "direct")));

        var pipeline = builder.Build();
        var context = CreateContext();

        var result = await pipeline(context);

        result.Output.Should().Be("direct");
    }

    [Fact]
    public async Task Pipeline_MiddlewareCanAccessExecutionContext()
    {
        var builder = new AgentPipelineBuilder();
        builder.Use(new ContextWritingMiddleware("tag", "hello-from-middleware"));
        builder.UseTerminal(ctx =>
            {
                var val = ctx.Items.Get<string>("tag");
                return Task.FromResult(new AgentResult(true, val));
            });

        var pipeline = builder.Build();
        var context = CreateContext();

        var result = await pipeline(context);

        result.Output.Should().Be("hello-from-middleware");
    }

    [Fact]
    public async Task Pipeline_MiddlewareCanShortCircuit()
    {
        var order = new List<string>();

        var builder = new AgentPipelineBuilder();
        builder.Use(new TrackingMiddleware("A", order));
        builder.Use(new ShortCircuitMiddleware("Blocked!"));
        builder.Use(new TrackingMiddleware("B", order));
        builder.UseTerminal(_ =>
            {
                order.Add("Terminal");
                return Task.FromResult(new AgentResult(true, "done"));
            });

        var pipeline = builder.Build();
        var context = CreateContext();

        var result = await pipeline(context);

        result.Success.Should().BeFalse();
        result.Output.Should().Be("Blocked!");
        order.Should().BeEquivalentTo(["A-before", "A-after"], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Pipeline_MiddlewareCanTransformResult()
    {
        var builder = new AgentPipelineBuilder();
        builder.Use(new UppercaseMiddleware());
        builder.UseTerminal(_ => Task.FromResult(new AgentResult(true, "hello world")));

        var pipeline = builder.Build();
        var context = CreateContext();

        var result = await pipeline(context);

        result.Output.Should().Be("HELLO WORLD");
    }

    [Fact]
    public async Task Pipeline_RunsMiddlewareInRegistrationOrder()
    {
        var order = new List<string>();

        var builder = new AgentPipelineBuilder();
        builder.Use(new TrackingMiddleware("A", order));
        builder.Use(new TrackingMiddleware("B", order));
        builder.Use(new TrackingMiddleware("C", order));
        builder.UseTerminal(_ =>
            {
                order.Add("Terminal");
                return Task.FromResult(new AgentResult(true, "done"));
            });

        var pipeline = builder.Build();
        var context = CreateContext();

        var result = await pipeline(context);

        result.Success.Should().BeTrue();
        order.Should().BeEquivalentTo(
                ["A-before", "B-before", "C-before", "Terminal", "C-after", "B-after", "A-after"],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Pipeline_ThrowsWhenNoTerminal()
    {
        var builder = new AgentPipelineBuilder();
        builder.Use(new UppercaseMiddleware());

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*terminal*");
    }

    private sealed class ContextWritingMiddleware : IAgentPipelineMiddleware
    {
        private readonly string _key;

        private readonly string _value;

        public string Name => "ContextWriter";

        public ContextWritingMiddleware(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public async Task<AgentResult> InvokeAsync(
            IExecutionContext context,
            AgentPipelineDelegate next)
        {
            context.Items.Set(_key, _value);
            return await next(context);
        }
    }

    private static DefaultExecutionContext CreateContext()
    {
        var request = new AgentRequest("test");
        var options = new AgentRuntimeOptions();
        var agentContext = new DefaultAgentContext
                               {
                                   Goal = "test",
                                   State = new AgentState(),
                                   Memory = new InMemoryAgentMemory()
                               };
        return DefaultExecutionContext.Create(request, options, agentContext);
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

    private sealed class ShortCircuitMiddleware : IAgentPipelineMiddleware
    {
        private readonly string _output;

        public string Name => "ShortCircuit";

        public ShortCircuitMiddleware(string output)
        {
            _output = output;
        }

        public Task<AgentResult> InvokeAsync(IExecutionContext context, AgentPipelineDelegate next)
        {
            return Task.FromResult(new AgentResult(false, _output));
        }
    }

    private sealed class TrackingMiddleware : IAgentPipelineMiddleware
    {
        private readonly string _name;

        private readonly List<string> _order;

        public string Name => _name;

        public TrackingMiddleware(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public async Task<AgentResult> InvokeAsync(
            IExecutionContext context,
            AgentPipelineDelegate next)
        {
            _order.Add($"{_name}-before");
            var result = await next(context);
            _order.Add($"{_name}-after");
            return result;
        }
    }

    private sealed class UppercaseMiddleware : IAgentPipelineMiddleware
    {
        public string Name => "Uppercase";

        public async Task<AgentResult> InvokeAsync(
            IExecutionContext context,
            AgentPipelineDelegate next)
        {
            var result = await next(context);
            return result with { Output = result.Output?.ToUpperInvariant() };
        }
    }
}
