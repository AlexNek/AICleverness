using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// LLM call strategy that uses the non-streaming <see cref="ILlmClient.CompleteAsync"/>
/// with a wall-clock timeout. Behavior is identical to the original inline code
/// in <see cref="LlmToolLoop"/> prior to the strategy extraction.
/// </summary>
internal sealed class BufferedLlmCallStrategy : ILlmCallStrategy
{
    private readonly ILlmClient _llm;

    private readonly ILogger<BufferedLlmCallStrategy>? _logger;

    public BufferedLlmCallStrategy(ILlmClient llm, ILogger<BufferedLlmCallStrategy>? logger = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(strategyOptions.CompletionTimeoutSeconds));

        try
        {
            return await _llm.CompleteAsync(messages, tools, options, timeoutCts.Token);
        }
        catch (OperationCanceledException ocEx) when (
            timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var message =
                $"LLM buffered completion timeout: no response received within {strategyOptions.CompletionTimeoutSeconds}s (model may be unavailable or overloaded)";

            _logger?.LogWarning(
                ocEx,
                "Buffered completion timeout after {Seconds}s. Original exception: {OriginalMessage}",
                strategyOptions.CompletionTimeoutSeconds,
                ocEx.InnerException?.Message ?? ocEx.Message);

            throw new OperationCanceledException(message, ocEx);
        }
    }
}
