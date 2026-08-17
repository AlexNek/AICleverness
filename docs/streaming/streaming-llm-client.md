# Streaming LLM Client

## Overview

The LLM tool loop calls your provider in one of two modes:

| | Buffered | Streaming |
|---|---|---|
| Interface | `ILlmClient` | `IStreamingLlmClient : ILlmClient` |
| Method | `CompleteAsync` — one call, one response | `StreamAsync` — tokens arrive one by one |
| Timeout | Wall-clock only (`CompletionTimeoutSeconds`) | Idle-based (`IdleTimeoutSeconds`) + absolute cap (`CompletionTimeoutSeconds`) |
| Failure mode | Timeout kills a working model | Idle stall (no meaningful chunk) or absolute cap reached |

The runtime picks the mode automatically. If your client implements
`IStreamingLlmClient`, the streaming path activates. Otherwise the
buffered path is used — no configuration needed.

This page covers the streaming path in detail. For the basic
`ILlmClient` contract, see [LLM Client](../concepts/llm-client.md).

## IStreamingLlmClient

```csharp
public interface IStreamingLlmClient : ILlmClient
{
    IAsyncEnumerable<LlmChunk> StreamAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

Because `IStreamingLlmClient` inherits from `ILlmClient`, every streaming implementation is **guaranteed** to also provide `CompleteAsync` as a non-streaming fallback. This is enforced at compile time.

Implementations should apply `[EnumeratorCancellation]` to the `CancellationToken` parameter on their async-iterator method to ensure `WithCancellation()` propagation works correctly.

## Timeout Semantics

Two distinct timeout properties control behavior:

| Property | Default | Applies to | Meaning |
|----------|---------|-----------|---------|
| `CompletionTimeoutSeconds` | 60s | Both strategies | Absolute wall-clock cap. For buffered calls: total allowed duration. For streaming calls: safety cap — kills the stream regardless of chunk activity. |
| `IdleTimeoutSeconds` | 30s | Streaming only | Silence threshold between meaningful chunks. If no meaningful chunk arrives within this duration, the stream is considered stalled. |

### Examples

- **Buffered client, response in 45s** → Succeeds (within 60s cap).
- **Streaming client, chunks every 5s for 90s, cap at 120s** → Succeeds. Idle timer resets on each chunk. `CompletionTimeoutSeconds` must exceed the total generation time.
- **Streaming client, chunks every 5s for 90s, cap at 60s** → Fails. Absolute cap kills the stream at 60s despite continuous chunk activity.
- **Streaming client, 35s silence** → Fails. Idle timeout (30s) fires. Classified as `TransientAdvance`, triggers failover.

## Chunk Validation Rules

A chunk resets the idle timer only if it carries **meaningful payload**:

| Condition | Resets idle timer? |
|-----------|-------------------|
| `Content` is non-null and non-empty | ✅ Yes |
| `ToolCalls` has entries | ✅ Yes |
| `IsCompleted` is true | ✅ Yes |
| All fields null/empty, `IsCompleted = false` (keep-alive) | ❌ No |

Empty SSE comments or heartbeat frames from providers do NOT mask a stalled model.

## Partial Failure Behavior

A turn either produces a **complete** `LlmResponse` or fails entirely:

- If the stream throws an exception after some chunks have been received (network drop, provider error) and `IsCompleted` was never observed → **full failure**. No partial content is returned.
- The failure is classified via `ILlmErrorClassifier` exactly like a non-streaming failure.
- Failover proceeds to the next candidate. The same streaming strategy is used — the client instance handles all models, so the strategy is fixed for the loop's lifetime.

## Tool-Call Delta Accumulation

LLMs stream tool calls as incremental JSON fragments. The `LlmToolCallDelta` record carries:

```csharp
public sealed record LlmToolCallDelta(
    int Index,       // position identifying which tool call
    string? Id,      // tool call ID (first fragment only)
    string? Name,    // function name (first fragment only)
    string? ArgumentsFragment);  // JSON fragment to append
```

The `StreamingToolCallAccumulator` accumulates fragments by index:

1. First delta for index 0: `{ Index: 0, Id: "c1", Name: "search", ArgumentsFragment: "{\"q\":" }`
2. Second delta for index 0: `{ Index: 0, ArgumentsFragment: "\"hello\"}" }`
3. `Build()` returns: `[LlmToolCall("c1", "search", "{\"q\":\"hello\"}")]`

Multiple concurrent tool calls are tracked independently by their index.

## Token Usage

Usage data in streaming is **best-effort**:

- The `LlmChunk` record includes an optional `Usage` field.
- The aggregator takes the **last non-null** `Usage` from any chunk.
- If no chunk provides usage, the resulting `LlmResponse.Usage` is null.
- Callers must tolerate null usage (already required by the existing contract).

## Observer and Event Contract

- `OnLlmRespondedAsync` is called **once** with the final aggregated `LlmResponse` — not per-chunk.
- Per-chunk observation is via `ModelChunkEvent` with `IsFinal = false`, emitted through the event emitter during streaming runs.
- The final aggregated response still emits `ModelChunkEvent` with `IsFinal = true` as before.
- Observers within the streaming loop are awaited inline. Slow observers block chunk consumption. Observers should be fast by contract.

## Strategy Pattern

The `LlmToolLoop` delegates LLM calls to an `ILlmCallStrategy`:

```text
ILlmCallStrategy
├── BufferedLlmCallStrategy   (CompleteAsync + wall-clock CancelAfter)
└── StreamingLlmCallStrategy  (StreamAsync + idle timer + absolute cap)
```

The strategy is resolved at construction time by `LlmCallStrategyFactory`:

- If the injected `ILlmClient` implements `IStreamingLlmClient` → `StreamingLlmCallStrategy`
- Otherwise → `BufferedLlmCallStrategy`

No runtime `is` type checks occur inside the tool loop. This satisfies the Open/Closed Principle.

## Failover Integration

When a streaming call fails and failover activates:

- The same `ILlmClient` instance handles all models (model name comes from `LlmCompletionOptions`).
- The strategy remains the same across failover candidates (same client capabilities).
- `ModelFailoverHandler` is unaware of streaming — it only sees success/failure.

## Timeout Diagnostics

When a timeout fires, both strategies produce descriptive error messages
and preserve the original provider exception:

### Streaming strategy

| Condition | Error message | Failover verb |
|-----------|---------------|---------------|
| **Zero meaningful chunks received** | `LLM streaming: no response received within Ns (model may be unavailable or overloaded)` | `unavailable` |
| **Some chunks received, then silence** | `LLM streaming idle timeout: no meaningful chunk received for Ns (after M chunks)` | `timed out` |

### Buffered strategy (non-streaming `ILlmClient`)

| Condition | Error message | Failover verb |
|-----------|---------------|---------------|
| **No response within wall-clock cap** | `LLM buffered completion timeout: no response received within Ns (model may be unavailable or overloaded)` | `unavailable` |

In both cases, the original `OperationCanceledException` (and its
`InnerException` if any) is preserved — so the provider's actual error
message (e.g. "high demand, come later") surfaces in logs.

### Logging

Both `StreamingLlmCallStrategy` and `BufferedLlmCallStrategy` accept a
typed `ILogger<T>` (created via `ILoggerFactory` in `LlmCallStrategyFactory`).
On timeout they log:

- The timeout duration
- The number of chunks received before failure (streaming only)
- The original exception message from the transport layer
- The inner exception (if any) — this is where provider-level errors
  (e.g. "service overloaded", HTTP 429/503 bodies) surface

To see these diagnostics, enable `Warning` level for:

- `AiCleverness.Runtime.StreamingLlmCallStrategy`
- `AiCleverness.Runtime.BufferedLlmCallStrategy`

### With DI container

When logging is registered in the container, `ILoggerFactory` is
automatically injected — no extra wiring needed:

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});
services.AddAiClevernessRuntime();
```

### Manual construction (no DI container)

When constructing `AgentRuntime` manually, pass an `ILoggerFactory`
to enable typed logging in internal components:

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddDebug();          // Output window in Visual Studio
    builder.SetMinimumLevel(LogLevel.Warning);
});

var runtime = new AgentRuntime(
    new MyLlmClient(),
    tools,
    loggerFactory: loggerFactory);
```

The `loggerFactory` parameter is optional. When omitted, the strategies
run without logging (existing behavior). When provided, `AgentRuntime`
and each internal component create their own typed logger from the
factory — log entries show the originating class name as the category.

!!! tip
    `builder.AddDebug()` sends log output to the Visual Studio Output
    window. For file logging, add Serilog or NLog via their respective
    `ILoggerFactory` extensions.
