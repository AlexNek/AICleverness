# Installation

AiCleverness is one NuGet package:

```bash
dotnet add package AiCleverness
```

You need .NET 10.0 or later.

## Dependencies

The package has very few dependencies:

- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions` (only needed if you use
  `AddHostedAgentRuntime`)
- `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` (only needed
  if you use health checks)

The package contains **no AI provider SDK**. AiCleverness never talks to a
provider directly. Instead it calls your own
[ILlmClient](../concepts/llm-client.md) implementation. Your client can use
OpenAI, Anthropic, Ollama, a local model — anything.

## What You Get

The package already contains working defaults for the most common parts:

| Part | What the package provides |
| --- | --- |
| The runtime | `IAgentRuntime` / `IStreamingAgentRuntime` with a default implementation |
| Tool execution | A default executor with timeout and retries. Protection against running the same tool call twice is not part of the default — it is opt-in with `AddIdempotencyCache()` and `IdempotentToolExecutor` (see [Tool Idempotency](../tools/tool-idempotency.md)) |
| Memory | In-memory implementations for all memory tiers |
| Persistence | In-memory checkpoint store and execution journal |
| Observability | Collectors for metrics, diagnostics, and startup analysis |

You can replace every one of these parts with your own implementation —
they are all interfaces in `AiCleverness.Abstractions`. See
[Dependency Injection](dependency-injection.md) for how everything is
connected.
