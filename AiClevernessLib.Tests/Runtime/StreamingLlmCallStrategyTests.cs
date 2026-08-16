using System.Diagnostics;
using System.Runtime.CompilerServices;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class StreamingLlmCallStrategyTests
{
    [Fact]
    public async Task CallAsync_MeaningfulChunks_ResetIdleTimer_Succeeds()
    {
        // Arrange — chunks arrive every 200ms, idle timeout is 1s
        var chunks = new List<LlmChunk>
        {
            new("Hello ", IsCompleted: false),
            new("world", IsCompleted: false),
            new(null, IsCompleted: true)
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.FromMilliseconds(200));
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 1);

        // Act
        var response = await strategy.CallAsync([], null, null, opts, CancellationToken.None);

        // Assert
        response.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task CallAsync_IdleTimeout_ThrowsOperationCanceled()
    {
        // Arrange — single chunk then long silence (no IsCompleted)
        var chunks = new List<LlmChunk>
        {
            new("start", IsCompleted: false)
            // No more chunks — stream ends without IsCompleted
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.Zero, delayAfterLast: TimeSpan.FromSeconds(5));
        var strategy = new StreamingLlmCallStrategy(client);
        // 100ms idle timeout will trigger before the 5s delay
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 1);

        // Act — the idle check happens on the NEXT iteration; since there are no
        // more chunks, the enumeration ends and no idle exception fires.
        // To properly test idle timeout mid-stream, we need chunks that come too slowly.
        var slowChunks = new List<LlmChunk>
        {
            new("first", IsCompleted: false),
            new("second", IsCompleted: false), // arrives after 2s
            new(null, IsCompleted: true) // arrives after another 2s
        };
        var slowClient = new FakeStreamingLlmClient(slowChunks, delayBetweenChunks: TimeSpan.FromSeconds(2));
        var slowStrategy = new StreamingLlmCallStrategy(slowClient);
        var tightOpts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 1);

        // Act & Assert — second chunk arrives after 2s but idle timeout is 1s
        var act = () => slowStrategy.CallAsync([], null, null, tightOpts, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*idle timeout*");
    }

    [Fact]
    public async Task CallAsync_EmptyChunks_DoNotResetIdleTimer()
    {
        // Arrange — empty chunks arrive frequently but no meaningful content
        var chunks = new List<LlmChunk>
        {
            new(null, IsCompleted: false), // empty keep-alive
            new("", IsCompleted: false), // empty string keep-alive
            new(null, IsCompleted: false), // another empty
            new("content", IsCompleted: true) // meaningful but arrives after idle threshold
        };
        // Delay between chunks is 400ms, idle timeout is 1s.
        // Empty chunks don't reset timer, so after 3 empty chunks (1.2s total),
        // idle threshold of 1s is exceeded before the meaningful "content" chunk.
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.FromMilliseconds(400));
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 1);

        // Act & Assert
        var act = () => strategy.CallAsync([], null, null, opts, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*idle timeout*");
    }

    [Fact]
    public async Task CallAsync_AbsoluteTimeoutCap_KillsLongStream()
    {
        // Arrange — chunks arrive every 100ms (within idle timeout) but total exceeds cap
        var chunks = new List<LlmChunk>();
        for (var i = 0; i < 50; i++)
            chunks.Add(new LlmChunk($"chunk{i}", IsCompleted: false));
        chunks.Add(new LlmChunk(null, IsCompleted: true));

        // 100ms between chunks, 50 chunks = 5s total. Cap at 2s.
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.FromMilliseconds(100));
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 2, IdleTimeoutSeconds: 5);

        // Act & Assert — absolute cap should cancel before stream completes
        var act = () => strategy.CallAsync([], null, null, opts, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CallAsync_UsageExtractedFromLastNonNullChunk()
    {
        // Arrange
        var chunks = new List<LlmChunk>
        {
            new("hello", Usage: null, IsCompleted: false),
            new(" ", Usage: new LlmTokenUsage(10, 5), IsCompleted: false),
            new("world", Usage: new LlmTokenUsage(20, 15), IsCompleted: false),
            new(null, IsCompleted: true)
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.Zero);
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 5);

        // Act
        var response = await strategy.CallAsync([], null, null, opts, CancellationToken.None);

        // Assert — takes the last non-null usage
        response.Usage.Should().NotBeNull();
        response.Usage!.PromptTokens.Should().Be(20);
        response.Usage.CompletionTokens.Should().Be(15);
    }

    [Fact]
    public async Task CallAsync_OnChunkCallback_InvokedForMeaningfulChunks()
    {
        // Arrange
        var chunks = new List<LlmChunk>
        {
            new("Hello", IsCompleted: false),
            new(null, IsCompleted: false), // empty — no callback
            new(" World", IsCompleted: false),
            new(null, IsCompleted: true)
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.Zero);
        var strategy = new StreamingLlmCallStrategy(client);
        var receivedChunks = new List<string>();
        var opts = new LlmCallStrategyOptions(
            CompletionTimeoutSeconds: 30,
            IdleTimeoutSeconds: 5,
            OnChunk: content => receivedChunks.Add(content));

        // Act
        await strategy.CallAsync([], null, null, opts, CancellationToken.None);

        // Assert
        receivedChunks.Should().Equal("Hello", " World");
    }

    [Fact]
    public async Task CallAsync_ToolCallDeltas_AccumulatedIntoResponse()
    {
        // Arrange
        var chunks = new List<LlmChunk>
        {
            new(null, ToolCalls: [new LlmToolCallDelta(0, Id: "c1", Name: "search", ArgumentsFragment: "{\"q\":")]),
            new(null, ToolCalls: [new LlmToolCallDelta(0, ArgumentsFragment: "\"AI\"}")]),
            new(null, IsCompleted: true)
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.Zero);
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 5);

        // Act
        var response = await strategy.CallAsync([], null, null, opts, CancellationToken.None);

        // Assert
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls![0].Id.Should().Be("c1");
        response.ToolCalls[0].Name.Should().Be("search");
        response.ToolCalls[0].Arguments.Should().Be("{\"q\":\"AI\"}");
    }

    [Fact]
    public async Task CallAsync_IsCompletedWithNullContent_ReturnsNullContent()
    {
        // Arrange
        var chunks = new List<LlmChunk>
        {
            new(null, IsCompleted: true)
        };
        var client = new FakeStreamingLlmClient(chunks, delayBetweenChunks: TimeSpan.Zero);
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 5);

        // Act
        var response = await strategy.CallAsync([], null, null, opts, CancellationToken.None);

        // Assert
        response.Content.Should().BeNull();
        response.ToolCalls.Should().BeNull();
        response.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task CallAsync_MidStreamException_ThrowsWithoutPartialContent()
    {
        // Arrange
        var client = new ExplodingStreamingLlmClient(chunksBeforeExplosion: 2);
        var strategy = new StreamingLlmCallStrategy(client);
        var opts = new LlmCallStrategyOptions(CompletionTimeoutSeconds: 30, IdleTimeoutSeconds: 5);

        // Act & Assert
        var act = () => strategy.CallAsync([], null, null, opts, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stream exploded!");
    }

    // --- Test Doubles ---

    private sealed class FakeStreamingLlmClient : IStreamingLlmClient
    {
        private readonly IReadOnlyList<LlmChunk> _chunks;
        private readonly TimeSpan _delayBetweenChunks;
        private readonly TimeSpan _delayAfterLast;

        public FakeStreamingLlmClient(
            IReadOnlyList<LlmChunk> chunks,
            TimeSpan delayBetweenChunks,
            TimeSpan? delayAfterLast = null)
        {
            _chunks = chunks;
            _delayBetweenChunks = delayBetweenChunks;
            _delayAfterLast = delayAfterLast ?? TimeSpan.Zero;
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use StreamAsync.");
        }

        public async IAsyncEnumerable<LlmChunk> StreamAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _chunks.Count; i++)
            {
                if (i > 0 && _delayBetweenChunks > TimeSpan.Zero)
                    await Task.Delay(_delayBetweenChunks, cancellationToken);

                yield return _chunks[i];
            }

            if (_delayAfterLast > TimeSpan.Zero)
                await Task.Delay(_delayAfterLast, cancellationToken);
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
            throw new NotSupportedException("Use StreamAsync.");
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
}
