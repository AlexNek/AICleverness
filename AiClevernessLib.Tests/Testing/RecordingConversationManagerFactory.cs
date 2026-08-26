using AiCleverness.Abstractions;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingConversationManagerFactory : IConversationManagerFactory
{
    public List<RecordingConversationManager> Created { get; } = [];

    public IConversationManager Create()
    {
        var manager = new RecordingConversationManager();
        Created.Add(manager);
        return manager;
    }
}