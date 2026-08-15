# Quick Start

You need two things to run an agent, and one optional thing:

1. An `ILlmClient` — the connection to your AI provider.
2. DI registration — connect everything to the runtime.
3. *(Optional)* At least one `ITool` — something the agent can do. Tools
   are only needed for runs where the model may call tools. For an
   explicit tool-free run, pass `AllowedToolNames: []` — the model then
   answers directly with text.

## 1. Implement ILlmClient — talk to your AI provider

This class sends messages to your AI and returns the answer. Write it once;
the runtime never talks to the provider itself.

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

## 2. Implement ITool — something the agent can do (optional)

Skip this step if your run needs no tools. A tool has a name, a
description, and a parameter schema. The model reads
these three things and decides when to call the tool.

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

## 3. Connect everything and run

Register the runtime, your client, and your tool in DI. Then take the runtime
and give it a goal:

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

What happens inside `RunAsync`: the runtime sends the goal and the tool
definitions to your LLM client, runs the tool calls the model asks for, and
repeats this until the goal is answered. See
[Runtime Pipeline](../concepts/runtime-pipeline.md) for every step of this
loop, and [Defining Tools](../tools/defining-tools.md) for more ways to
describe a tool.
