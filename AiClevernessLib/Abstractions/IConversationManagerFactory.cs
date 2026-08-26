namespace AiCleverness.Abstractions;

/// <summary>Creates a conversation manager scoped to one decision-tree execution.</summary>
public interface IConversationManagerFactory
{
    IConversationManager Create();
}