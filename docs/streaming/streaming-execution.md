# Streaming Execution

Normally you call `RunAsync` and wait for the final answer. With streaming,
you see what happens **while** the run is still working: parts of the
model's text, finished tool calls, and the final result — one event after
the other.

Use `IStreamingAgentRuntime` and loop over the events:

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

All events are subtypes of the `AgentEvent` record. The most important
ones:

- `ModelChunkEvent` — a part of the model's text output.
- `ToolCompletedEvent` — a tool call has finished; contains its output.
- `ExecutionCompletedEvent` — the run is done; contains the final
  `AgentResult`.

Every event carries the execution id. If several runs stream at the same
time, you can tell their events apart by this id.

## Streaming Providers

Some providers send tool calls as many small JSON parts. If your
`ILlmClient` receives such parts, use the
[streaming tool buffer](tool-call-buffer.md) to put them back together
before you return the response.
