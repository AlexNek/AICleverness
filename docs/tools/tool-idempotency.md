# Tool Idempotency

Idempotency here means: **a successful tool call never runs twice in the
same run**.

Why is this needed? A quality gate can reject the model's answer and force
a retry. During the retry, the model may ask for the same tool call again.
For tools with real side effects — sending mail, creating records, charging
money — this is dangerous.

The idempotency layer solves this for successful calls. It remembers the
result of every successful tool call. If the same call comes again, it
returns the stored result instead of running the tool a second time.

**Failed calls are different.** A failed call is never stored, so it may
run again. Usually this is what you want. But a call can fail *after* its
side effect already happened — for example: the mail was sent, but the
confirmation timed out. If your tool has this risk, make the tool itself
safe: give the call its own idempotency key (a business transaction id),
or check the outcome transactionally before doing the work again.

## Wiring

```csharp
services.AddIdempotencyCache();

// Wrap the executor used by AgentRuntime with idempotency.
// The runtime can then probe this wrapper before reporting a real call.
var idempotentExecutor = new IdempotentToolExecutor(
    inner: defaultExecutor,
    cache: cache,
    executionScope: executionId);
```

`AddIdempotencyCache()` registers the cache and its default in-memory
implementation. It does not automatically replace a configured `IToolExecutor`.
To enable the runtime's pre-invocation replay behavior, supply the
`IdempotentToolExecutor` (or another executor implementing
`ICacheAwareToolExecutor`) as the runtime's `toolExecutor`.

## How It Works

- **The runtime probes before real-call reporting.** When the configured
  executor implements `ICacheAwareToolExecutor`, the loop checks for a cached
  result after the model decision and before `Calling tool ...`,
  `ToolStartedEvent`, observers, bus events, or the invocation counter.
- **A cache hit is not reported as execution.** The runtime emits a distinct
  progress step such as `  fetch_url reused cached result: ...`, suppresses
  normal invocation and completion callbacks/events, and sends the complete
  cached output to the next LLM turn.
- **Cache misses use the normal path.** The existing `Calling tool ...` message,
  counter, observers, bus events, streaming lifecycle events, and completion
  behavior remain unchanged.
- **Only successful calls are stored.** A failed call is always executed
  again. A cache-aware executor may still return a cached failure, which is
  reported as a reused cached failure.
- **The cache belongs to one run.** A second run never sees the stored
  results of the first run when it uses a different execution scope.
- **The key is the `InvocationId`** if the call has one.
- **Without an `InvocationId`**, the key is built from the tool name and
  its arguments (SHA256 of the sorted arguments). So the same tool with the
  same arguments counts as the same call.

The cache probe and subsequent execution are separate operations. This
behavior does not provide atomic get-or-execute coordination for concurrent
identical callers.

The default cache lives in memory (`InMemoryIdempotencyCache`). For several
servers that share one cache, implement `IIdempotencyCache` with your own
distributed store.
