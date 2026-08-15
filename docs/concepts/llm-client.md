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
