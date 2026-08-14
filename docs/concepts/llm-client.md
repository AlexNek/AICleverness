# LLM Client

`ILlmClient` is the only thing you must implement. It wraps any LLM API into
a common contract; the runtime never knows which provider sits behind it.

```csharp
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,      // conversation history
        IReadOnlyList<ToolDefinition>? tools,     // tool schemas (null = no tools)
        LlmCompletionOptions? options,            // temperature, model, etc.
        CancellationToken ct);
}
```

## The Response

`LlmResponse` carries three things:

- `Content` — the text response
- `ToolCalls` — requested tool invocations (the runtime handles the loop —
  you never execute tools yourself here)
- `Usage` — token counts (`LlmTokenUsage` with `PromptTokens` and
  `CompletionTokens`)

## Messages

The conversation history is a list of `LlmMessage` records with `Role`,
`Content`, `ToolCalls`, and `ToolCallId`. Tool results flow back to the model
as messages, so streaming providers that split tool calls into partial JSON
chunks can be supported through the
[streaming tool buffer](../streaming/tool-call-buffer.md).

## Registration

```csharp
services.AddAiClevernessLlmClient<MyLlmClient>();
```

For provider-neutral capability checks, model selection, and prompt
management, see the capability and conversation abstractions in the
[API Reference](../api-reference/interfaces.md).
