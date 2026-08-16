using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// LLM call strategy that uses the non-streaming <see cref="ILlmClient.CompleteAsync"/>
/// with a wall-clock timeout. Behavior is identical to the original inline code
/// in <see cref="LlmToolLoop"/> prior to the strategy extraction.
/// </summary>
internal sealed class BufferedLlmCallStrategy : ILlmCallStrategy
{
    private readonly ILlmClient _llm;

    public BufferedLlmCallStrategy(ILlmClient llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
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

        return await _llm.CompleteAsync(messages, tools, options, timeoutCts.Token);
    }
}
