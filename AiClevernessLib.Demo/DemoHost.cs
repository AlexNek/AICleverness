using AiCleverness.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Demo;

/// <summary>
/// Composition root helper that builds a fully configured demo service provider.
/// </summary>
internal static class DemoHost
{
    /// <summary>
    /// Creates a provider with the runtime, the scripted LLM, and the demo tool.
    /// Extra registrations (policies, strategies, gates, observers) can be added
    /// through <paramref name="configure"/>.
    /// </summary>
    public static ServiceProvider CreateProvider(
        ScriptedLlmClient llm,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddSingleton(llm);
        services.AddSingleton<ILlmClient>(llm);
        services.AddAgentTool<WeatherTool>();
        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        RegisterToolsFromDi(provider);
        return provider;
    }

    // ToolRegistry keeps its own store, so DI-registered tools must be copied into it.
    private static void RegisterToolsFromDi(IServiceProvider provider)
    {
        var registry = provider.GetRequiredService<IToolRegistry>();
        foreach (var tool in provider.GetServices<ITool>())
        {
            registry.Register(tool);
        }
    }
}
