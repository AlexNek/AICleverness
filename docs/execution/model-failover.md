# Model Failover

When an LLM call fails with a classified transient error—such as a
completion timeout, rate limit, provider overload, or capacity failure—the
runtime can fail over to the next candidate model in the chain — same
conversation, same turn, no repeated tool calls.

## How It Works

1. **Candidate chain** — model resolution produces an ordered list of
   fallback models alongside the primary pick.
2. **Transient failure** — the error classifier decides whether a failure
   is transient (advance to next model) or permanent (abort).
3. **In-place switch** — the runtime swaps the model, rebuilds options, and
   retries the same turn. The conversation history is unchanged.
4. **Observability** — every attempt and every switch is observable through
   observers, streaming events, and bus events.

## Opt-In

Failover is **disabled by default**. Enable it globally or per-request:

```csharp
// Global: in DI setup
services.AddAiClevernessRuntime(options =>
{
    options.EnableModelFailover = true;
});

// Per-request: override in parameters
var request = new AgentRequest(
    Goal: "Research pricing for provider X",
    Parameters: new Dictionary<string, object>
    {
        ["enable_model_failover"] = true,
        ["model_fallback_chain"] = new[] { "gpt-4o", "claude-3.5-sonnet" }
    });
```

## Candidate Chain Sources

The chain is resolved in this priority order:

| Source | When used |
| --- | --- |
| `model_fallback_chain` request parameter | Explicit list of model names — takes precedence |
| `ModelResolutionResult.Fallbacks` | Built automatically by capability resolution |
| Empty (no failover) | No chain available or failover disabled |

When using an explicit chain and a model catalog is registered, model
names are validated against the catalog. Unknown names are skipped with a
warning log. Without a catalog, names are passed through as-is. In both
cases the chain is normalized: the active model is excluded and duplicates
are removed, so failover never retries the current model.

## Pinned Models

If you set a specific model via `AgentPropertyKeys.Model` without providing
a fallback chain, the model is considered **pinned**. Failover is disabled
regardless of the enable flag. Pinned means pinned.

## Turn Budget

A failed attempt does **not** count against `maxTurns`. The turn counter is
rewound on failover so consumers see the same budget semantics as before —
the retry reuses the same logical turn in the loop counter, the execution
state, and the event stream (no second `TurnStartedEvent` for that turn).

## Chain Exhaustion

When all candidates in the chain have been tried and failed, the run fails
with an error message:

```text
LLM failover chain exhausted after 3 attempts; last model tried: 'model-c' on turn 0
```

This is identifiable programmatically via `FailureEvent.Phase == "ModelFailover"`.

## Event Sequence

For a single failover on turn N, consumers observe this exact sequence:

```text
1. TurnStartedEvent          { Turn = N }
2. OnLlmCalledAsync          (messages sent to model A)
3. [timeout / error]
4. OnLlmCallCompletedAsync   { Model = A, Success = false, Classification = TransientAdvance }
5. FailureEvent              { Phase = "LlmCompletion", IsTransient = true }
6. OnModelSwitchedAsync      { from = A, to = B, reason = "..." }
7. ModelSwitchedAgentEvent   { From = A, To = B }
8. ModelSwitchedBusEvent     { From = A, To = B }
9. OnLlmCalledAsync          (same messages sent to model B)
10. [success]
11. OnLlmCallCompletedAsync  { Model = B, Success = true }
```

The retried attempt reuses the logical turn — there is no second
`TurnStartedEvent` and no extra turn counted in the execution state.

## Observability Hooks

| Channel | Event | When |
| --- | --- | --- |
| Observer | `OnLlmCallCompletedAsync(LlmCallInfo)` | Every attempt (success, error, timeout) |
| Observer | `OnModelSwitchedAsync(from, to, reason)` | Every model switch |
| Streaming | `FailureEvent` | Every failed attempt, including transient attempts that advance |
| Streaming | `ModelSwitchedAgentEvent` | Every model switch |
| Bus | `ModelSwitchedBusEvent` | Every model switch |

## Example: Capability-Routed Failover

With two capability profiles registered (a fast text model and a slower
fallback), resolution automatically builds the chain:

```csharp
// Profile 1: fast model (priority 1)
// Profile 2: slower model (priority 2)
// Resolution picks the fast model as primary, slower as fallback.

services.AddAiClevernessRuntime(options =>
{
    options.EnableModelFailover = true;
    options.DefaultCompletionTimeoutSeconds = 10;
});

// When the fast model times out, the runtime switches to the slower model
// transparently — the consumer sees a single successful result.
```

## Error Classification

The shared `DefaultLlmErrorClassifier` classifies failures without performing
retries itself. `TransientAdvance` allows the failover handler to advance to
the next candidate; `Permanent` stops the current run.

| Failure | Classification | Action |
| --- | --- | --- |
| Per-turn timeout (`OperationCanceledException`, caller token alive) | `TransientAdvance` | Advance to next candidate |
| Caller cancellation, including a provider exception wrapping cancellation | `Permanent` | Abort |
| Explicit provider `IsTransient = true` | `TransientAdvance` | Advance to next candidate |
| Explicit provider `IsTransient = false` | `Permanent` | Abort |
| Configured provider error or status mapping | Configured value | Advance or abort |
| HTTP 408, 429, 500, 502, 503, or 504 | `TransientAdvance` | Advance to next candidate |
| HTTP 4xx, 501, 505, or other HTTP 5xx | `Permanent` | Abort |
| Unclassified provider error code or status | `Permanent` | Abort |

The core package does not contain provider names or provider error-code
vocabulary. Applications can provide mappings at composition time:

```csharp
using System.Net;
using AiCleverness.Models;

services.AddAiClevernessRuntime(
    configure: options =>
    {
        options.EnableModelFailover = true;
    },
    configureFailureClassification: options =>
    {
        options.ProviderErrorMappings[
            new LlmProviderErrorKey("example-provider", "capacity_exceeded")] =
            EFailureClassification.TransientAdvance;

        options.ProviderStatusMappings[
            new LlmProviderStatusKey("example-provider", (HttpStatusCode)529)] =
            EFailureClassification.TransientAdvance;
    });
```

Mappings are case-insensitive and are consulted only when the adapter leaves
`LlmProviderException.IsTransient` unset. Explicit adapter classification wins;
caller cancellation and hard-permanent statuses always remain permanent. This
allows each application or provider adapter package to own its vocabulary
without adding provider policy to the core package.

Provider adapters can preserve their original exception and expose structured
metadata without depending on provider-specific runtime types:

```csharp
throw new LlmProviderException(
    providerException,
    provider: "example-provider",
    errorCode: "capacity_exceeded",
    statusCode: (HttpStatusCode)529,
    retryAfter: TimeSpan.FromSeconds(10));
```

`RetryAfter` is diagnostic only; the runtime does not delay or retry the same
model. Hard-permanent HTTP statuses take precedence over conflicting transient
metadata, and legacy `HTTP nnn` and rate-limit message patterns remain
supported for adapters that have not migrated.

## Provider Failure Diagnostics

When a provider exception is present, the same immutable
`LlmProviderFailureMetadata` snapshot is available on `LlmCallInfo.ProviderFailure`,
`LlmCallCompletedBusEvent.ProviderFailure`, and streaming
`FailureEvent.ProviderFailure` for every failed attempt, including transient
attempts that advance successfully and terminal failures. These properties are
nullable and remain null for successful calls and legacy exceptions without
provider metadata.

## Interaction with Quality Gates

Quality gates operate above the tool loop. If a quality gate rejects the
result and triggers a retry, the entire loop restarts with a fresh turn
counter. The candidate chain is re-resolved from the current context state.
`ModelExecutionInfo.Attempt` is preserved across quality retries.
