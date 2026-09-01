# AiCleverness — Provider-Neutral AI Execution Runtime for .NET

AiCleverness is a lightweight execution runtime for building AI-powered .NET applications. Agents are one execution pattern; the core value is orchestration: policies, planning, deterministic strategies, tool execution, quality gates, transformations, and observability around any provider adapter.

**[Read the developer manual](https://alexnek.github.io/AICleverness/)** — getting started guides, core concepts, tool authoring, streaming, memory tiers, security, workflows, and a full API reference.

## Installation

```bash
dotnet add package AiCleverness
```

Requires .NET 10.0 or later. Zero external AI provider SDKs — bring your own.

## What You Can Build

| Use case | What the runtime gives you | Docs |
| --- | --- | --- |
| Tool-calling assistant | LLM tool loop with discovery, idempotent execution, and compensation | [Tools](https://alexnek.github.io/AICleverness/tools/defining-tools/) |
| Guarded production agent | Prompt guards, human-in-the-loop approval, output validation | [Security](https://alexnek.github.io/AICleverness/security/security-approval/) |
| Quality-controlled output | Quality gates evaluate every result and force retries until it meets your bar | [Quality Gates](https://alexnek.github.io/AICleverness/execution/quality-gates/) |
| Real-time chat UX | Stream execution events as `IAsyncEnumerable<AgentEvent>` | [Streaming](https://alexnek.github.io/AICleverness/streaming/streaming-execution/) |
| Multi-agent pipelines | DAG workflows with explicit dependencies and data flow | [Workflows](https://alexnek.github.io/AICleverness/workflows/multi-agent/) |
| Declarative decision workflows | JSON decision trees with bounded questions, actions, predicates, budgets, and verdicts | [Decision Trees](https://alexnek.github.io/AICleverness/execution/decision-trees/) |
| Context-aware agents | Tiered memory — working, long-term, vector — behind one aggregate interface | [Memory Tiers](https://alexnek.github.io/AICleverness/memory/memory-tiers/) |
| Cost-efficient routing | Deterministic strategies bypass the LLM; planners decompose complex goals | [Policies and Strategies](https://alexnek.github.io/AICleverness/concepts/policies-strategies/) |
| Auditable AI services | Structured metrics and diagnostic traces for every run | [Observability](https://alexnek.github.io/AICleverness/operations/observability/) |

## Quick Start

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models;

var services = new ServiceCollection();
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();   // your provider adapter
services.AddAgentTool<WeatherTool>();                // your tool

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IAgentRuntime>();

var result = await runtime.RunAsync(new AgentRequest(
    Goal: "What is the weather in Tokyo?",
    AllowedToolNames: ["get_weather"]));

Console.WriteLine(result.Output);
```

Three pieces — `ILlmClient`, `ITool`, DI wiring — and you have a working execution runtime.

## Transcripts and decision-tree diagnostics

Transcript persistence is opt-in. For ordinary agent runs, configure an
absolute directory in the request and provide a redactor in
`AgentRuntimeOptions`:

```csharp
services.AddAiClevernessRuntime(options =>
{
    options.TranscriptRedactor = text => text;
});

var request = new AgentRequest(
    Goal: "Summarize the evidence",
    Parameters: new Dictionary<string, object>
    {
        [AgentPropertyKeys.MarkdownTranscriptDirectory] =
            Path.GetFullPath("transcripts")
    });
```

Normal transcripts pass persisted content through the host redactor. Debug
transcripts are explicitly unredacted and should be used only with controlled
data and access. The built-in Markdown builder and file sink work without
additional setup once the directory and redactor are configured.

Decision-tree applications configure `DecisionTreeExecutionOptions` with an
absolute `TranscriptDirectory`, optional `DecisionTranscriptPolicy` limits,
and the same redaction/debug choices. Both agent and decision-tree options
support per-execution `TranscriptBuilderFactory` and `TranscriptSinkFactory`
delegates for JSON/HTML/structured formatting or non-file destinations such as
databases and queues. Factories must create fresh builder and sink instances;
never return cached mutable transcript objects or register them as singletons.
Transcript failures are best effort and do not replace the primary execution
result. See the [full decision-transcript guide](https://alexnek.github.io/AICleverness/execution/decision-trees/#decision-transcripts) for action outcome summaries, readable node names, policy limits, logical sink paths, and failure semantics.

## Full Reference

The repository README contains the complete reference: core concepts,
full DI setup, models, streaming, memory tiers, security, workflows,
persistence, observability, and agent scoping —
[github.com/AlexNek/AICleverness](https://github.com/AlexNek/AICleverness).

- [Developer manual](https://alexnek.github.io/AICleverness/)
- [API reference](https://alexnek.github.io/AICleverness/api-reference/interfaces/)
- [Changelog](https://github.com/AlexNek/AICleverness/blob/master/CHANGELOG.md)

## License

MIT
