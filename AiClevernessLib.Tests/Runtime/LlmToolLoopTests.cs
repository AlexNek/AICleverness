using System.Runtime.CompilerServices;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

/// <summary>
/// Integration tests for <see cref="LlmToolLoop"/> streaming behavior.
/// Exercises the streaming strategy through the full <see cref="AgentRuntime"/> pipeline.
/// </summary>
public sealed class LlmToolLoopTests
{
    [Fact]
    public async Task Streaming_SlowChunks_IdleTimerResets_Succeeds()
    {
        // Arrange — chunks arrive every 500ms, idle timeout 1s
        var llm = new StreamingLlmClient();
        llm.SetupStreaming("model-a", new List<LlmChunk>
        {
            new("a", IsCompleted: false),
            new("b", IsCompleted: false),
            new("c", IsCompleted: false),
            new("d", IsCompleted: false),
            new("e", IsCompleted: false),
            new("f", IsCompleted: false),
            new(null, IsCompleted: true)
        }, delayBetweenChunks: TimeSpan.FromMilliseconds(500));

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 1,
                DefaultCompletionTimeoutSeconds = 30
            });

        var request = new AgentRequest(
            "test streaming",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — idle timer resets on each chunk, run succeeds
        result.Success.Should().BeTrue();
        result.Output.Should().Be("abcdef");
    }

    [Fact]
    public async Task Streaming_IdleTimeout_TriggersFailover()
    {
        // Arrange — first chunk arrives, then 1.5s silence (idle timeout 500ms)
        var llm = new StreamingLlmClient();
        llm.SetupStreaming("model-a", new List<LlmChunk>
        {
            new("start", IsCompleted: false),
            new("end", IsCompleted: true) // arrives after 1.5s, exceeds idle timeout
        }, delayBetweenChunks: TimeSpan.FromMilliseconds(1500));

        llm.SetupStreaming("model-b", new List<LlmChunk>
        {
            new("success from b", IsCompleted: true)
        }, delayBetweenChunks: TimeSpan.Zero);

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 1,
                DefaultCompletionTimeoutSeconds = 30,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test idle timeout",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — idle timeout triggers, failover to model-b succeeds
        result.Success.Should().BeTrue();
        result.Output.Should().Be("success from b");
        llm.CallLog.Should().HaveCount(2);
        llm.CallLog[0].Model.Should().Be("model-a");
        llm.CallLog[1].Model.Should().Be("model-b");
    }

    [Fact]
    public async Task NonStreaming_BufferedStrategy_WallClockTimeout()
    {
        // Arrange — non-streaming client (only implements ILlmClient)
        var llm = new NonStreamingLlmClient();
        llm.SetupResponse("model-a", new LlmResponse("buffered response"));

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultCompletionTimeoutSeconds = 5
            });

        var request = new AgentRequest(
            "test buffered",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — buffered strategy uses wall-clock timeout
        result.Success.Should().BeTrue();
        result.Output.Should().Be("buffered response");
    }

    [Fact]
    public async Task Streaming_MidStreamException_NoPartialContent()
    {
        // Arrange — streaming client throws mid-stream
        var llm = new StreamingLlmClient();
        llm.SetupStreamingWithException("model-a", chunksBeforeException: 2);

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });

        var request = new AgentRequest(
            "test mid-stream exception",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — no partial content returned
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.FailureKind.Should().Be(EFailureKind.LlmError);
    }

    [Fact]
    public async Task Streaming_EmptyChunks_IdleTimerNotReset_TimesOut()
    {
        // Arrange — empty chunks arrive frequently but no meaningful content
        var llm = new StreamingLlmClient();
        llm.SetupStreaming("model-a", new List<LlmChunk>
        {
            new(null, IsCompleted: false), // empty keep-alive
            new("", IsCompleted: false), // empty string keep-alive
            new(null, IsCompleted: false), // another empty
            new(null, IsCompleted: false), // another empty
            new("content", IsCompleted: true) // meaningful but arrives too late
        }, delayBetweenChunks: TimeSpan.FromMilliseconds(400));

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 1,
                DefaultCompletionTimeoutSeconds = 30
            });

        var request = new AgentRequest(
            "test empty chunks",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — empty chunks don't reset idle timer, times out
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
    }

    [Fact]
    public async Task Streaming_IsCompletedWithNullContent_ValidCompletion()
    {
        // Arrange — stream ends with IsCompleted=true but no content
        var llm = new StreamingLlmClient();
        llm.SetupStreaming("model-a", new List<LlmChunk>
        {
            new(null, IsCompleted: true)
        }, delayBetweenChunks: TimeSpan.Zero);

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 30
            });

        var request = new AgentRequest(
            "test null content",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — valid completion with null content, loop exhausts turns
        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(EFailureKind.TurnLimitExceeded);
    }

    [Fact]
    public async Task Streaming_Failover_UsesSameStrategy()
    {
        // Arrange — streaming client times out on model-a (idle timeout), succeeds on model-b.
        // Both models are on the same client instance — strategy is fixed for the loop's lifetime.
        var llm = new StreamingLlmClient();
        llm.SetupStreaming("model-a", new List<LlmChunk>
        {
            new("start", IsCompleted: false),
            new("should-not-arrive", IsCompleted: true) // arrives after 10s, far exceeds idle timeout
        }, delayBetweenChunks: TimeSpan.FromSeconds(10));

        llm.SetupStreaming("model-b", new List<LlmChunk>
        {
            new("success", IsCompleted: true)
        }, delayBetweenChunks: TimeSpan.Zero);

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 1,
                DefaultCompletionTimeoutSeconds = 30,
                EnableModelFailover = true
            });

        var request = new AgentRequest(
            "test failover strategy",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a",
                [AgentPropertyKeys.ModelFallbackChain] = new List<string> { "model-b" },
                [AgentPropertyKeys.EnableModelFailover] = true
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — failover uses same streaming strategy, second call succeeds
        result.Success.Should().BeTrue();
        result.Output.Should().Be("success");
        llm.CallLog.Should().HaveCount(2);
        llm.CallLog[0].Model.Should().Be("model-a");
        llm.CallLog[1].Model.Should().Be("model-b");
    }

    [Fact]
    public async Task Streaming_AbsoluteSafetyCap_KillsLongStream()
    {
        // Arrange — chunks arrive every 100ms (within idle timeout) but total exceeds cap
        var llm = new StreamingLlmClient();
        var chunks = new List<LlmChunk>();
        for (var i = 0; i < 30; i++)
            chunks.Add(new LlmChunk($"chunk{i}", IsCompleted: false));
        chunks.Add(new LlmChunk(null, IsCompleted: true));

        llm.SetupStreaming("model-a", chunks, delayBetweenChunks: TimeSpan.FromMilliseconds(100));

        var tools = new ToolRegistry();
        var runtime = new AgentRuntime(
            llm,
            tools,
            options: new AgentRuntimeOptions
            {
                DefaultIdleTimeoutSeconds = 5,
                DefaultCompletionTimeoutSeconds = 1 // absolute cap
            });

        var request = new AgentRequest(
            "test absolute cap",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.Model] = "model-a"
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert — absolute cap kills stream despite continuous chunks
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
    }

    // --- Test doubles ---

    private sealed class StreamingLlmClient : IStreamingLlmClient
    {
        private readonly Dictionary<string, List<LlmChunk>> _streamScripts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TimeSpan> _streamDelays = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (int chunksBeforeException, string exceptionMessage)> _exceptionScripts = new(StringComparer.OrdinalIgnoreCase);

        public List<(string Model, IReadOnlyList<LlmMessage> Messages)> CallLog { get; } = [];

        public void SetupStreaming(string model, List<LlmChunk> chunks, TimeSpan delayBetweenChunks)
        {
            _streamScripts[model] = chunks;
            _streamDelays[model] = delayBetweenChunks;
        }

        public void SetupStreamingWithException(string model, int chunksBeforeException, string exceptionMessage = "Stream exploded!")
        {
            _exceptionScripts[model] = (chunksBeforeException, exceptionMessage);
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use StreamAsync for streaming client.");
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var model = options?.Model ?? "unknown";
            CallLog.Add((model, messages));

            if (_exceptionScripts.TryGetValue(model, out var exceptionConfig))
            {
                for (var i = 0; i < exceptionConfig.chunksBeforeException; i++)
                {
                    yield return new LlmChunk($"chunk{i}", IsCompleted: false);
                    await Task.Yield();
                }
                throw new InvalidOperationException(exceptionConfig.exceptionMessage);
            }

            if (_streamScripts.TryGetValue(model, out var chunks))
            {
                var delay = _streamDelays.GetValueOrDefault(model, TimeSpan.Zero);
                for (var i = 0; i < chunks.Count; i++)
                {
                    if (i > 0 && delay > TimeSpan.Zero)
                        await Task.Delay(delay, cancellationToken);

                    yield return chunks[i];
                }
            }
            else
            {
                yield return new LlmChunk($"response from {model}", IsCompleted: true);
            }
        }
    }

    private sealed class NonStreamingLlmClient : ILlmClient
    {
        private readonly Dictionary<string, LlmResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

        public void SetupResponse(string model, LlmResponse response)
        {
            _responses[model] = response;
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var model = options?.Model ?? "unknown";
            if (_responses.TryGetValue(model, out var response))
                return Task.FromResult(response);

            return Task.FromResult(new LlmResponse($"response from {model}"));
        }
    }
}
