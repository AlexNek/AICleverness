# Tool Idempotency

Quality gates can request retries of the LLM loop — and a retry may issue the
same tool call again. For side-effecting tools (sending mail, creating
records, charging) that duplication must be prevented. The idempotency layer
caches successful results per execution and replays them instead of running
the tool twice.

## Wiring

```csharp
services.AddIdempotencyCache();

// Wrap the tool executor with idempotency
var idempotentExecutor = new IdempotentToolExecutor(
    inner: defaultExecutor,
    cache: cache,
    executionScope: executionId);
```

## Key Behaviors

- **Cache on success only** — failed calls always retry
- **Per-execution scope** — no cross-execution cache pollution
- **Explicit InvocationId** takes priority over the semantic key
- **Semantic fallback** — tool name + SHA256(sorted arguments) when no
  `InvocationId` is present

The default cache is in-memory (`InMemoryIdempotencyCache`); implement
`IIdempotencyCache` for a distributed store.
