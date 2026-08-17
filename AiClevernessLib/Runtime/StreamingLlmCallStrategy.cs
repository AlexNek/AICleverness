using System.Text;

using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// LLM call strategy that uses <see cref="IStreamingLlmClient.StreamAsync"/>
/// with idle-based timeout (reset on every meaningful chunk) and an absolute safety cap.
/// </summary>
internal sealed class StreamingLlmCallStrategy : ILlmCallStrategy
{
    private readonly ILogger<StreamingLlmCallStrategy>? _logger;

    private readonly IStreamingLlmClient _streamingClient;

    public StreamingLlmCallStrategy(IStreamingLlmClient streamingClient, ILogger<StreamingLlmCallStrategy>? logger = null)
    {
        _streamingClient = streamingClient ?? throw new ArgumentNullException(nameof(streamingClient));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> CallAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        LlmCompletionOptions? options,
        LlmCallStrategyOptions strategyOptions,
        CancellationToken cancellationToken)
    {
        var idleTimeout = TimeSpan.FromSeconds(strategyOptions.IdleTimeoutSeconds);
        var absoluteTimeout = TimeSpan.FromSeconds(strategyOptions.CompletionTimeoutSeconds);

        // Absolute safety cap — kills the stream regardless of chunk activity.
        using var absoluteCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        absoluteCts.CancelAfter(absoluteTimeout);

        // Idle timeout — cancelled if no meaningful chunk arrives within the threshold.
        // Linked into the stream token so it can interrupt a stalled wait between chunks.
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(absoluteCts.Token);
        idleCts.CancelAfter(idleTimeout);

        var contentBuilder = new StringBuilder();
        var toolCallAccumulator = new StreamingToolCallAccumulator();
        LlmTokenUsage? lastUsage = null;
        string? finishReason = null;
        var chunksReceived = 0;

        try
        {
            await foreach (var chunk in _streamingClient
                               .StreamAsync(messages, tools, options, idleCts.Token)
                               .ConfigureAwait(false))
            {
                var isMeaningful = false;

                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    contentBuilder.Append(chunk.Content);
                    strategyOptions.OnChunk?.Invoke(chunk.Content);
                    isMeaningful = true;
                }

                if (chunk.ToolCalls is { Count: > 0 })
                {
                    toolCallAccumulator.AddDeltas(chunk.ToolCalls);
                    isMeaningful = true;
                }

                if (chunk.Usage is not null)
                {
                    lastUsage = chunk.Usage;
                }

                if (chunk.IsCompleted)
                {
                    isMeaningful = true;
                    finishReason = "stop";
                    break;
                }

                // Restart idle timer only on meaningful chunks.
                if (isMeaningful)
                {
                    chunksReceived++;
                    idleCts.CancelAfter(idleTimeout);
                }
            }
        }
        catch (OperationCanceledException ocEx) when (
            absoluteCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var message = chunksReceived == 0
                ? $"LLM streaming: no response received within {strategyOptions.CompletionTimeoutSeconds}s (model may be unavailable or overloaded)"
                : $"LLM streaming completion timeout: total duration exceeded {strategyOptions.CompletionTimeoutSeconds}s after {chunksReceived} chunks";

            _logger?.LogWarning(
                ocEx,
                "Streaming absolute timeout after {Seconds}s (chunks received: {ChunksReceived}). Original exception: {OriginalMessage}",
                strategyOptions.CompletionTimeoutSeconds,
                chunksReceived,
                ocEx.InnerException?.Message ?? ocEx.Message);

            throw new OperationCanceledException(message, ocEx);
        }
        catch (OperationCanceledException ocEx) when (
            idleCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var message = chunksReceived == 0
                ? $"LLM streaming: no response received within {strategyOptions.IdleTimeoutSeconds}s (model may be unavailable or overloaded)"
                : $"LLM streaming idle timeout: no meaningful chunk received for {strategyOptions.IdleTimeoutSeconds}s (after {chunksReceived} chunks)";

            _logger?.LogWarning(
                ocEx,
                "Streaming idle timeout after {Seconds}s (chunks received: {ChunksReceived}). Original exception: {OriginalMessage}",
                strategyOptions.IdleTimeoutSeconds,
                chunksReceived,
                ocEx.InnerException?.Message ?? ocEx.Message);

            throw new OperationCanceledException(message, ocEx);
        }

        var content = contentBuilder.Length > 0 ? contentBuilder.ToString() : null;
        var toolCalls = toolCallAccumulator.HasEntries ? toolCallAccumulator.Build() : null;

        return new LlmResponse(content, toolCalls, finishReason, lastUsage);
    }
}
