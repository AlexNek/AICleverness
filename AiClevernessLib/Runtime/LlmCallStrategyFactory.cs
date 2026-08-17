using AiCleverness.Abstractions;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Resolves the appropriate <see cref="ILlmCallStrategy"/> based on
/// the capabilities of the provided <see cref="ILlmClient"/>.
/// </summary>
internal static class LlmCallStrategyFactory
{
    /// <summary>
    /// Creates a strategy for the given LLM client.
    /// Returns <see cref="StreamingLlmCallStrategy"/> if the client implements
    /// <see cref="IStreamingLlmClient"/>; otherwise returns <see cref="BufferedLlmCallStrategy"/>.
    /// </summary>
    public static ILlmCallStrategy Create(ILlmClient llm, ILoggerFactory? loggerFactory = null)
    {
        if (llm is IStreamingLlmClient streamingClient)
            return new StreamingLlmCallStrategy(
                streamingClient,
                loggerFactory?.CreateLogger<StreamingLlmCallStrategy>());

        return new BufferedLlmCallStrategy(
            llm,
            loggerFactory?.CreateLogger<BufferedLlmCallStrategy>());
    }
}
