using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingConversationManager : IConversationManager
{
    private readonly List<LlmMessage> _messages = [];

    public int EstimatedTokenCount => _messages.Sum(message => message.Content?.Length ?? 0) / 4;

    public int MessageCount => _messages.Count;

    public void AddMessage(LlmMessage message) => _messages.Add(message);

    public void AddMessages(IEnumerable<LlmMessage> messages) => _messages.AddRange(messages);

    public void Clear() => _messages.Clear();

    public IReadOnlyList<LlmMessage> GetMessages() => _messages.AsReadOnly();

    public Task<IReadOnlyList<LlmMessage>> GetMessagesForCompletionAsync(
        int maxTokens,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LlmMessage>>(_messages.AsReadOnly());
}