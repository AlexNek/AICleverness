# Quick Start

Three pieces — an `ILlmClient`, at least one `ITool`, and DI wiring — and you
have a working execution runtime.

## 1. Implement ILlmClient — talk to any AI provider

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models;

public sealed class MyLlmClient : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        LlmCompletionOptions? options,
        CancellationToken ct)
    {
        // Call OpenAI, Anthropic, Ollama, or your local model
        var content = await CallMyAiAsync(messages, ct);
        return new LlmResponse(content);
    }
}
```

## 2. Implement ITool — something the agent can do

```csharp
public sealed class WeatherTool : ITool
{
    public string Name => "get_weather";
    public string Description => "Get current weather for a city";
    public ToolDefinition Definition => new(Name, Description, """{
        "type": "object",
        "properties": {
            "city": { "type": "string" }
        },
        "required": ["city"]
    }""");

    public async Task<ToolResult> InvokeAsync(
        ToolInvocation invocation, CancellationToken ct)
    {
        var city = invocation.Arguments["city"]?.ToString();
        var temp = await FetchTemperatureAsync(city, ct);
        return new ToolResult(true, $"Temperature in {city}: {temp}°C", null);
    }
}
```

## 3. Wire it up and run

```csharp
var services = new ServiceCollection();
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
services.AddAgentTool<WeatherTool>();

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IAgentRuntime>();

var request = new AgentRequest(
    Goal: "What is the weather in Tokyo?",
    AllowedToolNames: ["get_weather"]);

var result = await runtime.RunAsync(request);
Console.WriteLine(result.Output);                  // "Temperature in Tokyo: 22°C"
Console.WriteLine(string.Join("\n", result.Steps)); // execution log
```

The runtime sends the goal and tool definitions to your LLM client, executes
the tool calls it requests, and loops until the goal is answered. See
[Runtime Pipeline](../concepts/runtime-pipeline.md) for every stage of that
loop, and [Defining Tools](../tools/defining-tools.md) for richer tool
metadata.
