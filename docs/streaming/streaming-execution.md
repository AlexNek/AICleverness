# Streaming Execution

`IStreamingAgentRuntime` provides real-time execution events via
`IAsyncEnumerable<AgentEvent>` — model text chunks, completed tool calls,
and lifecycle events as they happen.

```csharp
var runtime = provider.GetRequiredService<IStreamingAgentRuntime>();

await foreach (var evt in runtime.RunStreamingAsync(request))
{
    switch (evt)
    {
        case ModelChunkEvent chunk:
            Console.Write(chunk.Content);
            break;
        case ToolCompletedEvent tool:
            Console.WriteLine($"[Tool] {tool.ToolName}: {tool.Output}");
            break;
        case ExecutionCompletedEvent done:
            Console.WriteLine($"\nDone: {done.Result.Output}");
            break;
    }
}
```

## Event Types

`AgentEvent` is the base record; concrete subtypes cover the execution
lifecycle, including:

- `ModelChunkEvent` — partial model output text
- `ToolCompletedEvent` — a tool call finished with its output
- `ExecutionCompletedEvent` — the run finished with the final `AgentResult`

All events carry the execution id, so concurrent streams can be
demultiplexed.

## Streaming Providers

If your `ILlmClient` implementation receives tool calls as partial JSON
chunks, use the [streaming tool buffer](tool-call-buffer.md) to accumulate
them into complete invocations before returning the response.
