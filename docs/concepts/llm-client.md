# LLM Client

`ILlmClient` is the connection between the library and your LLM provider.
It is the **only** interface you must implement. The runtime calls this
interface and never knows which provider is behind it — OpenAI, a local
model, or anything else.

```csharp
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,      // the conversation so far
        IReadOnlyList<ToolDefinition>? tools,     // the tool list (null = no tools)
        LlmCompletionOptions? options,            // temperature, model name, etc.
        CancellationToken ct);
}
```

The parameters:

- `messages` — the conversation history: the goal, the model's earlier
  answers, and the results of earlier tool calls.
- `tools` — the tools the model may call this time. `null` means no tools.
- `options` — settings for this call, for example the model name and the
  temperature.

## The Response

`LlmResponse` carries three things:

- `Content` — the text answer of the model.
- `ToolCalls` — the tools the model wants to call. You do **not** run these
  tools yourself: the runtime runs them and calls your client again with the
  results.
- `Usage` — how many tokens the call used (`PromptTokens` and
  `CompletionTokens` inside `LlmTokenUsage`).

## Messages

The conversation history is a list of `LlmMessage` records. Each message has
a `Role` (for example `user`, `assistant`, or `tool`), a `Content`, and —
for tool traffic — `ToolCalls` and `ToolCallId`.

Tool results go back to the model as messages. Some streaming providers
split a tool call into many small JSON parts. To put these parts back
together, see the
[streaming tool buffer](../streaming/tool-call-buffer.md).

## Registration

Register your client in DI with one line:

```csharp
services.AddAiClevernessLlmClient<MyLlmClient>();
```

For provider-neutral capability checks, model selection, and prompt
management, see the capability and conversation interfaces in the
[API Reference](../api-reference/interfaces.md).

## Buffered vs Streaming

The runtime supports two ways to call the LLM. Which one it uses depends
on whether your client also implements `IStreamingLlmClient`:

| | Buffered (default) | Streaming (opt-in) |
|---|---|---|
| Interface | `ILlmClient` | `IStreamingLlmClient : ILlmClient` |
| Method | `CompleteAsync` — waits for the full response | `StreamAsync` — returns tokens one by one |
| Timeout | Wall-clock (`CompletionTimeoutSeconds`) | Idle-based (`IdleTimeoutSeconds`) with an absolute cap |
| Best for | Simple providers, short responses | Slow models, long generations, real-time UX |

**Buffered mode** is what you get with `ILlmClient` alone. The runtime
sends the request, waits for the full response, and applies a wall-clock
timeout. If the model takes longer than `CompletionTimeoutSeconds`, the
call is killed — even if the model is still working.

**Streaming mode** activates when your client also implements
`IStreamingLlmClient`. The runtime reads tokens as they arrive and resets
an idle timer on every meaningful chunk. A slow model that keeps sending
tokens is never killed for being slow — only a real stall (no meaningful
chunk for `IdleTimeoutSeconds`) triggers a timeout. `CompletionTimeoutSeconds`
remains as an absolute safety cap to prevent infinite streams.

The runtime chooses the mode automatically at construction time. You do
not switch between them at runtime — the same strategy is used for every
call in a run, including failover retries.

To implement streaming for your provider, see
[Streaming LLM Client](../streaming/streaming-llm-client.md).
