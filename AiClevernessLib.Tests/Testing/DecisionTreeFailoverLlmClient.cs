using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

internal sealed class DecisionTreeFailoverLlmClient : ILlmClient
{
    private readonly Queue<Func<LlmCompletionOptions?, Task<LlmResponse>>> _scripts;

    public DecisionTreeFailoverLlmClient(
        params Func<LlmCompletionOptions?, Task<LlmResponse>>[] scripts)
    {
        _scripts = new Queue<Func<LlmCompletionOptions?, Task<LlmResponse>>>(scripts);
    }

    public List<string?> RequestedModels { get; } = [];

    public async Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        RequestedModels.Add(options?.Model);
        return await _scripts.Dequeue()(options);
    }
}
