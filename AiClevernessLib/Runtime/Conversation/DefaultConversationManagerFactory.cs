using AiCleverness.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace AiCleverness.Runtime.Conversation;

/// <summary>Resolves a fresh conversation manager for each execution.</summary>
public sealed class DefaultConversationManagerFactory : IConversationManagerFactory
{
    private readonly IServiceProvider _services;

    public DefaultConversationManagerFactory(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IConversationManager Create()
        => _services.GetRequiredService<IConversationManager>();
}