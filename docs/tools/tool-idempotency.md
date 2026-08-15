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

// Wrap the tool executor with idempotency
var idempotentExecutor = new IdempotentToolExecutor(
    inner: defaultExecutor,
    cache: cache,
    executionScope: executionId);
```

## How It Works

- **Only successful calls are stored.** A failed call is always executed
  again.
- **The cache belongs to one run.** A second run never sees the stored
  results of the first run.
- **The key is the `InvocationId`** if the call has one.
- **Without an `InvocationId`**, the key is built from the tool name and
  its arguments (SHA256 of the sorted arguments). So the same tool with the
  same arguments counts as the same call.

The default cache lives in memory (`InMemoryIdempotencyCache`). For several
servers that share one cache, implement `IIdempotencyCache` with your own
distributed store.
