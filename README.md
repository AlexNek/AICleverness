# AiCleverness — Provider-Neutral AI Execution Runtime for .NET

AiCleverness is a lightweight execution runtime for building AI-powered .NET applications. Agents are one execution pattern; the core value is orchestration: policies, planning, deterministic strategies, tool execution, quality gates, transformations, and observability around any provider adapter.

## Installation

```bash
dotnet add package AiCleverness
```

Requires .NET 10.0 or later. Zero external AI provider SDKs — bring your own.

## Why AiCleverness?

You want to build AI workflows that can use tools, follow rules, validate output, and stay provider-neutral. Without runtime abstractions, you end up with:

- Hardcoded LLM calls scattered across your codebase
- Tight coupling to one AI provider (OpenAI, Anthropic, etc.)
- No way to test decisions or policies in isolation
- Tool registration and discovery reinvented in every project
- Output validation, retries, and telemetry bolted on after the fact

AiCleverness defines **interfaces** for these concerns. Plug in any LLM provider, tool, policy, validator, transformer, or observer; the runtime orchestrates them without knowing provider-specific implementation details.

## Feature Overview

| Capability | Entry point | Description |
| --- | --- | --- |
| Orchestration | `IAgentRuntime` | Middleware pipeline: policies → planning → strategies → LLM loop → quality gates → validators → transformers |
| Streaming | `IStreamingAgentRuntime` | Real-time execution events via `IAsyncEnumerable<AgentEvent>` |
| Tools | `ITool`, `IToolExecutor` | Register tools, the runtime handles discovery and invocation |
| Policies | `IAgentPolicy` | Pre-execution guardrails that can block runs |
| Strategies | `IAgentStrategy` | Deterministic shortcuts bypassing the LLM |
| Planning | `IAgentPlanner` | Goal decomposition into steps before execution |
| Quality Gates | `IAgentQualityGate` | Output evaluation with retry support |
| Memory | `IWorkingMemory`, `ILongTermMemory`, `IVectorMemory` | Tiered memory with aggregate interface |
| Security | `IPromptGuard`, `IApprovalService`, `IScopeValidator` | Input/output guards, human-in-the-loop approval |
| Workflows | `WorkflowDefinition` | DAG-based multi-agent workflows |
| Observability | `IMetricsCollector`, `IDiagnosticCollector` | Structured metrics and diagnostic traces |
| DI | `AddAiClevernessRuntime()` | One-line `IServiceCollection` integration |

## Quick Start

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models;

var services = new ServiceCollection();
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
services.AddAgentTool<WeatherTool>();

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IAgentRuntime>();

var result = await runtime.RunAsync(new AgentRequest(
    Goal: "What is the weather in Tokyo?",
    AllowedToolNames: ["get_weather"]));

Console.WriteLine(result.Output);
```

## Documentation

The developer manual is published at
[alexnek.github.io/AICleverness](https://alexnek.github.io/AICleverness/).

For the full reference in one file — all interfaces, models, streaming,
memory tiers, security, workflows, and DI setup — see the
[library readme](AiClevernessLib/readme.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

MIT
