using System.Runtime.CompilerServices;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

/// <summary>
/// Integration tests verifying that <see cref="LlmToolLoop"/> correctly uses
/// streaming vs buffered strategies and handles idle/wall-clock timeouts.
/// </summary>
public sealed class StreamingLlmToolLoopTests
{
    [Fact]
    public async Task StreamingClient_SlowChunks_CompletesSuccessfully()
    {
        // Arrange — chunks every 200ms (well within 2s idle timeout), total ~1s
        var chunks = new List<LlmChunk>
        {
            new("The "),
            new("answer "),
            new("is 42", IsCompleted: true)
        };
        var llm = new FakeStreamingLlmClient(chunks, TimeSpan.FromMilliseconds(200));
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 2,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("What is the answer?");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("The answer is 42");
    }

    [Fact]
    public async Task StreamingClient_IdleTimeout_TriggersFailure()
    {
        // Arrange — first chunk arrives, then long silence (2s delay, 1s idle timeout)
        var chunks = new List<LlmChunk>
        {
            new("start"),
            new("stalled", IsCompleted: false) // arrives after 2s — beyond idle timeout
        };
        var llm = new FakeStreamingLlmClient(chunks, TimeSpan.FromSeconds(2));
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 1,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("stall test");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(EFailureKind.LlmTimeout);
    }

    [Fact]
    public async Task NonStreamingClient_WallClockTimeout_BehaviorUnchanged()
    {
        // Arrange — non-streaming client that takes too long
        var llm = new SlowBufferedLlmClient(TimeSpan.FromSeconds(5));
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 1,
                DefaultIdleTimeoutSeconds = 30
            });
        var request = new AgentRequest("slow buffered");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(EFailureKind.LlmTimeout);
    }

    [Fact]
    public async Task StreamingClient_MidStreamException_NoPartialContent()
    {
        // Arrange
        var llm = new ExplodingStreamingLlmClient(chunksBeforeExplosion: 2);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("explode");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.FailureKind.Should().Be(EFailureKind.Unknown);
    }

    [Fact]
    public async Task StreamingClient_EmitsModelChunkEvents()
    {
        // Arrange
        var chunks = new List<LlmChunk>
        {
            new("Hello"),
            new(" World", IsCompleted: true)
        };
        var llm = new FakeStreamingLlmClient(chunks, TimeSpan.Zero);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("stream events");

        // Act
        var events = new List<AgentEvent>();
        await foreach (var e in runtime.RunStreamingAsync(request))
        {
            events.Add(e);
        }

        // Assert — should have intermediate chunk events plus final
        var chunkEvents = events.OfType<ModelChunkEvent>().ToList();
        chunkEvents.Should().HaveCountGreaterThanOrEqualTo(1);

        // The final ModelChunkEvent should have IsFinal = true
        var finalChunk = chunkEvents.Last();
        finalChunk.IsFinal.Should().BeTrue();
        finalChunk.Content.Should().Be("Hello World");
    }

    [Fact]
    public async Task StreamingClient_CompletedWithNullContent_ValidEmptyResult()
    {
        // Arrange — immediate completion with no content
        var chunks = new List<LlmChunk>
        {
            new(null, IsCompleted: true)
        };
        var llm = new FakeStreamingLlmClient(chunks, TimeSpan.Zero);
        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("empty completion");

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — empty response triggers "no content and no tool calls" turn message
        // and eventually exhausts turns, OR the loop handles it.
        // With our implementation, null content produces a "Turn X produced no content" step.
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StreamingClient_WithToolCalls_DispatchesTools()
    {
        // Arrange — streaming response with tool calls, then a final text response
        var toolCallChunks = new List<LlmChunk>
        {
            new(null, ToolCalls: [new LlmToolCallDelta(0, Id: "c1", Name: "echo", ArgumentsFragment: "{\"message\":")]),
            new(null, ToolCalls: [new LlmToolCallDelta(0, ArgumentsFragment: "\"hi\"}")]),
            new(null, IsCompleted: true)
        };
        var finalChunks = new List<LlmChunk>
        {
            new("done", IsCompleted: true)
        };
        var llm = new MultiTurnStreamingLlmClient([toolCallChunks, finalChunks]);
        var tools = new ToolRegistry();
        tools.Register(new EchoTool());
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultMaxTurns = 5,
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });
        var request = new AgentRequest("use echo", ["echo"]);

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("done");
    }

    // --- Test Doubles ---

    private sealed class FakeStreamingLlmClient : IStreamingLlmClient
    {
        private readonly IReadOnlyList<LlmChunk> _chunks;
        private readonly TimeSpan _delay;

        public FakeStreamingLlmClient(IReadOnlyList<LlmChunk> chunks, TimeSpan delay)
        {
            _chunks = chunks;
            _delay = delay;
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming client — use StreamAsync.");
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _chunks.Count; i++)
            {
                if (i > 0 && _delay > TimeSpan.Zero)
                    await Task.Delay(_delay, cancellationToken);

                yield return _chunks[i];
            }
        }
    }

    private sealed class MultiTurnStreamingLlmClient : IStreamingLlmClient
    {
        private readonly Queue<IReadOnlyList<LlmChunk>> _turnChunks;

        public MultiTurnStreamingLlmClient(IEnumerable<IReadOnlyList<LlmChunk>> turns)
        {
            _turnChunks = new Queue<IReadOnlyList<LlmChunk>>(turns);
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming client — use StreamAsync.");
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var chunks = _turnChunks.Dequeue();
            foreach (var chunk in chunks)
            {
                yield return chunk;
                await Task.Yield();
            }
        }
    }

    private sealed class SlowBufferedLlmClient : ILlmClient
    {
        private readonly TimeSpan _delay;

        public SlowBufferedLlmClient(TimeSpan delay) => _delay = delay;

        public async Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            return new LlmResponse("slow response");
        }
    }

    private sealed class ExplodingStreamingLlmClient : IStreamingLlmClient
    {
        private readonly int _chunksBeforeExplosion;

        public ExplodingStreamingLlmClient(int chunksBeforeExplosion)
        {
            _chunksBeforeExplosion = chunksBeforeExplosion;
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming client — use StreamAsync.");
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _chunksBeforeExplosion; i++)
            {
                yield return new LlmChunk($"chunk{i}", IsCompleted: false);
                await Task.Yield();
            }

            throw new InvalidOperationException("Stream exploded!");
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
                    "properties": { "message": { "type": "string" } },
                    "required": ["message"]
                }
                """);

        public string Description => "Echoes a message.";
        public string Name => "echo";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            var msg = invocation.Arguments.TryGetValue("message", out var m) ? m?.ToString() : null;
            return Task.FromResult(new ToolResult(true, msg ?? "(empty)"));
        }
    }
}
