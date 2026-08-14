# Installation

AiCleverness is distributed as a single NuGet package:

```bash
dotnet add package AiCleverness
```

Requires .NET 10.0 or later.

## Dependencies

The library keeps the dependency footprint minimal:

- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions` (opt-in via `AddHostedAgentRuntime`)
- `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` (opt-in)

Zero external AI provider SDKs — bring your own. AiCleverness never talks to
a provider directly; it calls your [ILlmClient](../concepts/llm-client.md)
implementation, which can wrap OpenAI, Anthropic, Ollama, or a local model.

## What You Get

| Concern | Provided by |
| --- | --- |
| Orchestration | `IAgentRuntime` / `IStreamingAgentRuntime` with a default implementation |
| Tool execution | Default executor with timeout, retries, and idempotency |
| Memory | In-memory implementations for all memory tiers |
| Persistence | In-memory checkpoint store and execution journal |
| Observability | Metrics, diagnostics, and startup analysis collectors |

Everything is replaceable through the interfaces in
`AiCleverness.Abstractions` — see
[Dependency Injection](dependency-injection.md) for the full wiring picture.
