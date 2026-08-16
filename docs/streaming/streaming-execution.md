# Streaming Execution

There are two independent streaming levels in the library:

1. **Agent-event streaming** — the runtime exposes pipeline events
   (model text, tool completions, run result) as they happen. You see
   what the run does **while** it is still working.
2. **LLM-client streaming** — the runtime reads tokens from the LLM
   provider one by one instead of waiting for the full response. This
   enables idle-based timeouts and prevents false-positive kills of
   slow-but-working models.

They are independent: agent-event streaming works with both buffered and
streaming LLM clients. The sections below cover each level.

## Agent-Event Streaming

Normally you call `RunAsync` and wait for the final answer. With
agent-event streaming, you see what happens **while** the run is still
working: parts of the model's text, finished tool calls, and the final
result — one event after the other.

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
        case ToolCompletedAgentEvent tool:
            Console.WriteLine($"[Tool] {tool.ToolName}: {tool.Result.Output}");
            break;
        case RunCompletedEvent done:
            Console.WriteLine($"\nDone: {done.Result.Output}");
            break;
    }
}
```

## Event Types

All events are subtypes of the `AgentEvent` record. The most important
ones:

- `ModelChunkEvent` — a part of the model's text output.
- `ToolCompletedAgentEvent` — a tool call has finished; contains its output.
- `ModelSwitchedAgentEvent` — the runtime switched to a fallback model
  mid-execution due to a transient failure (see
  [model failover](../execution/model-failover.md)).
- `RunCompletedEvent` — the run is done; contains the final
  `AgentResult`.

Every event carries the execution id. If several runs stream at the same
time, you can tell their events apart by this id.

## Streaming Providers

Some providers send tool calls as many small JSON parts. If your
`ILlmClient` receives such parts, use the
[streaming tool buffer](tool-call-buffer.md) to put them back together
before you return the response.

## LLM-Client Streaming

The second streaming level is between the runtime and the LLM provider.
When your `ILlmClient` also implements `IStreamingLlmClient`, the
runtime reads tokens as they arrive instead of waiting for the full
response. This changes the timeout semantics:

- **Buffered client** — wall-clock timeout (`CompletionTimeoutSeconds`).
  A slow model is killed after the cap, even if it is still working.
- **Streaming client** — idle timeout (`IdleTimeoutSeconds`). The timer
  resets on every meaningful chunk. A slow model that keeps sending
  tokens is never killed for being slow. Only a real stall triggers a
  timeout. `CompletionTimeoutSeconds` remains as an absolute safety cap.

The runtime picks the mode automatically at construction time. The same
strategy is used for every call in a run, including failover retries.

For the full details — chunk validation, tool-call accumulation, failover
behavior — see [Streaming LLM Client](streaming-llm-client.md).
