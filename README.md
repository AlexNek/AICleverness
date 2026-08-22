# AiCleverness — Provider-Neutral AI Execution Runtime for .NET

AiCleverness is a lightweight execution runtime for building AI-powered .NET applications. Agents are one execution pattern; the core value is orchestration: policies, planning, deterministic strategies, tool execution, quality gates, transformations, and observability around any provider adapter.

**[📖 Read the developer manual](https://alexnek.github.io/AICleverness/)** — getting started guides, core concepts, tool authoring, streaming, memory tiers, security, workflows, and a full API reference.

## Installation

```bash
dotnet add package AiCleverness
```

Requires .NET 10.0 or later. Zero external AI provider SDKs — bring your own.

## What You Can Build

| Use case | What the runtime gives you | Docs |
| --- | --- | --- |
| Tool-calling assistant | LLM tool loop with discovery, idempotent execution, and compensation — you register tools, the runtime does the wiring | [Tools](https://alexnek.github.io/AICleverness/tools/defining-tools/) |
| Guarded production agent | Block dangerous prompts before the LLM, require human approval for sensitive tools, validate output before it reaches users | [Security](https://alexnek.github.io/AICleverness/security/security-approval/) |
| Quality-controlled output | Quality gates evaluate every result and force retries until it meets your bar — no "raw LLM output shipped as-is" | [Quality Gates](https://alexnek.github.io/AICleverness/execution/quality-gates/) |
| Real-time chat UX | Stream execution events — tokens, tool calls, decisions — as `IAsyncEnumerable<AgentEvent>` | [Streaming](https://alexnek.github.io/AICleverness/streaming/streaming-execution/) |
| Multi-agent pipelines | Compose specialized agents into DAG workflows with explicit dependencies and data flow | [Workflows](https://alexnek.github.io/AICleverness/workflows/multi-agent/) |
| Context-aware agents | Tiered memory — working, long-term, vector — behind one aggregate interface | [Memory Tiers](https://alexnek.github.io/AICleverness/memory/memory-tiers/) |
| Cost-efficient routing | Deterministic strategies answer routine requests without an LLM call; planners decompose complex goals first | [Policies and Strategies](https://alexnek.github.io/AICleverness/concepts/policies-strategies/) |
| Auditable AI services | Structured metrics and diagnostic traces for every run — know why each decision was made | [Observability](https://alexnek.github.io/AICleverness/operations/observability/) |

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

// 1. Implement ILlmClient — talk to any AI provider
public sealed class MyLlmClient : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        LlmCompletionOptions? options,
        CancellationToken ct)
    {
        // Call OpenAI, Anthropic, Ollama, or your local model
        var content = await CallMyAiAsync(messages, ct);
        return new LlmResponse(content);
    }
}

// 2. Implement ITool — something the agent can do
public sealed class WeatherTool : ITool
{
    public string Name => "get_weather";
    public string Description => "Get current weather for a city";
    public ToolDefinition Definition => new(Name, Description, """{
        "type": "object",
        "properties": {
            "city": { "type": "string" }
        },
        "required": ["city"]
    }""");

    public async Task<ToolResult> InvokeAsync(
        ToolInvocation invocation, CancellationToken ct)
    {
        var city = invocation.Arguments["city"]?.ToString();
        var temp = await FetchTemperatureAsync(city, ct);
        return new ToolResult(true, $"Temperature in {city}: {temp}°C", null);
    }
}

// 3. Wire it up with the default runtime
var services = new ServiceCollection();
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
services.AddAgentTool<WeatherTool>();

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IAgentRuntime>();

// 4. Run the agent
var request = new AgentRequest(
    Goal: "What is the weather in Tokyo?",
    AllowedToolNames: ["get_weather"]);

var result = await runtime.RunAsync(request);
Console.WriteLine(result.Output);       // "Temperature in Tokyo: 22°C"
Console.WriteLine(string.Join("\n", result.Steps));  // execution log
```

That's it. Three pieces (`ILlmClient`, `ITool`, DI wiring) and you have a working execution runtime.

## Demo transcript switches

The hermetic demo can exercise Markdown transcripts without any network access.
Use `/t` for normal transcript mode:

```powershell
dotnet run --project AiClevernessLib.Demo -- /t
```

Use `/d` for debug transcript mode:

```powershell
dotnet run --project AiClevernessLib.Demo -- /d
```

`--transcript` is an alias for `/t`; `--transcript-debug` and
`--debug-transcript` are aliases for `/d`. If both modes are supplied, debug
mode takes precedence. Both switches write transcripts under the executable
directory: `AiClevernessLib.Demo\bin\Debug\net10.0\transcripts` (or the
matching configuration/target-framework directory). Normal mode uses the
demo's safe identity redactor; `/d` explicitly enables unredacted debug
transcript output. The demo prints the resolved directory when transcript mode
is enabled. Library transcript filenames use a local timestamp plus a sanitized
human-readable task goal; execution IDs remain in transcript content, not in
filenames.

---

## Core Concepts

### ILlmClient — Your AI Provider Adapter

The only thing you must implement. Wraps any LLM API into a common contract.

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

The response contains:
- `Content` — text response
- `ToolCalls` — requested tool invocations (the runtime handles the loop)
- `Usage` — token counts

### ITool — What the Agent Can Do

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolDefinition Definition { get; }  // JSON schema for the LLM
    Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct);
}
```

Tools are **executors, not deciders**. They never decide what to do — they only perform work when called by the runtime.

Register them and the runtime handles discovery:

```csharp
services.AddAgentTool<WeatherTool>();
services.AddAgentTool<SearchTool>();
// or manually:
registry.Register(new WeatherTool(...));
```

### IAgentRuntime — The Orchestrator

Built into the library. Given a goal and tools, it:

1. Evaluates all registered **policies** (optional — block disallowed actions)
2. Runs the **planner** (optional — produce a step-by-step plan)
3. Tries each **strategy** (optional — deterministic shortcuts)
4. Falls back to the **LLM tool loop** — sends the goal + tool definitions to the LLM, receives tool calls, executes them through `IToolExecutor`, loops until done
5. Evaluates **quality gates** and can retry the LLM output with feedback
6. Runs simple **validators**, then ordered **transformers**
7. Notifies registered **observers** about lifecycle events

```csharp
var result = await runtime.RunAsync(new AgentRequest(
    Goal: "Find the API documentation for provider X",
    AllowedToolNames: ["search_web", "fetch_url"],
    Parameters: new Dictionary<string, object> { ["max_turns"] = 5 }
));

// Result: Success, Output, Reasoning, Steps, LlmTokenUsage, Metadata
```

### IToolExecutor — Tool Runtime Boundary

Tools stay focused on work. The executor owns cross-cutting runtime behavior such as timeout and retries.

```csharp
services.AddAgentToolExecutor<MyToolExecutor>(); // optional
```

Request parameters can tune the default executor:

```csharp
["tool_timeout_seconds"] = 30,
["tool_max_retries"] = 2
```

`ToolDefinition` now also supports metadata such as `Category`, `Version`, `CostPerCall`, `RequiresApproval`, `DefaultTimeout`, `Parallelizable`, `DangerLevel`, `Authentication`, and `Tags`.

### IAgentQualityGate — Output Quality

Quality gates evaluate the final result before it is returned. A gate can approve, reject, request a retry, or provide a replacement result.

```csharp
public sealed class JsonSchemaGate : IAgentQualityGate
{
    public string Name => "JsonSchema";
    public int Priority => 100;
    public bool AppliesTo(IAgentContext context) => true;

    public Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken ct)
    {
        var valid = IsValidJson(result.Output);
        return Task.FromResult(new QualityGateResult(
            Approved: valid,
            Retry: !valid,
            Reason: valid ? null : "Output must be valid JSON."));
    }
}

services.AddAgentQualityGate<JsonSchemaGate>();
```

Set `max_quality_retries` in request parameters to control retry attempts. The runtime feeds gate feedback into the next LLM attempt.

### Validators, Transformers, and Observers

Use validators for simple pass/fail checks, transformers for final formatting/redaction, and observers for telemetry.

```csharp
services.AddAgentResultValidator<MyValidator>();
services.AddAgentResultTransformer<PiiRedactor>();
services.AddAgentObserver<OpenTelemetryObserver>();
```

### IAgentPolicy — Rules and Guardrails

Policies evaluate context **before** execution. They can block the run entirely.

```csharp
public sealed class RateLimitPolicy : IAgentPolicy
{
    public string Name => "RateLimit";
    public int Priority => 100;    // higher = evaluated first
    public bool AppliesTo(IAgentContext context) => true;

    public async Task<PolicyResult> EvaluateAsync(IAgentContext context, CancellationToken ct)
    {
        if (await IsOverRateLimitAsync())
            return new PolicyResult(true, 0.0, "block", "Rate limit exceeded");
        return new PolicyResult(false, 1.0, "allow", null);
    }
}

// Register:
services.AddAgentPolicy<RateLimitPolicy>();
```

### IAgentStrategy — Deterministic Shortcuts

Strategies bypass the LLM for known scenarios. If a strategy succeeds, the runtime returns immediately without calling the LLM.

```csharp
public sealed class CachedResultStrategy : IAgentStrategy
{
    public string Name => "CachedResult";
    public bool CanExecute(IAgentContext context) => _cache.ContainsKey(context.Goal);
    public async Task<StrategyResult> ExecuteAsync(IAgentContext context, CancellationToken ct)
    {
        var cached = _cache[context.Goal];
        return new StrategyResult(true, cached);
    }
}

services.AddAgentStrategy<CachedResultStrategy>();
```

### IAgentPlanner — Step Generation

Optional. If registered, the runtime asks the planner to decompose the goal into steps before executing.

```csharp
services.AddDefaultPlanner();  // uses the LLM to plan
```

### IAgentMemory — Persistence

Key-value storage available to agents during execution. Default is in-memory; swap for Redis, SQLite, etc.

```csharp
public class RedisAgentMemory : IAgentMemory
{
    public async Task SaveAsync<T>(string key, T value, CancellationToken ct) { ... }
    public async Task<T?> LoadAsync<T>(string key, CancellationToken ct) { ... }
    public async Task<bool> ContainsAsync(string key, CancellationToken ct) { ... }
    public async Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct) { ... }
}
```

---

## Full DI Setup

```csharp
// Core runtime
services.AddAiClevernessRuntime(options =>
{
    options.DefaultMaxTurns = 10;
    options.DefaultCompletionTimeoutSeconds = 120;
    options.DefaultMaxQualityRetries = 2;
    options.DefaultToolMaxRetries = 1;
});
services.AddAiClevernessLlmClient<MyLlmClient>();

// Extension points
services.AddAgentPolicy<RateLimitPolicy>();
services.AddAgentStrategy<CachedResultStrategy>();
services.AddAgentQualityGate<JsonSchemaGate>();
services.AddAgentResultValidator<MyValidator>();
services.AddAgentResultTransformer<PiiRedactor>();
services.AddAgentObserver<RuntimeObserver>();

// Planning
services.AddDefaultPlanner();            // or AddSequentialPlanner()
services.AddNamedPlanner<CustomPlanner>();

// Tools
services.AddAgentTool<WeatherTool>();
services.AddAgentTool<SearchTool>();

// Persistence (opt-in)
services.AddInMemoryCheckpointStore();
services.AddInMemoryExecutionJournal();

// Hosting (opt-in)
services.AddHostedAgentRuntime(options =>
{
    options.MaxConcurrentExecutions = 4;
    options.GracePeriodSeconds = 30;
});

// Observability (opt-in)
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();
```

When logging is registered (`services.AddLogging()`), `ILoggerFactory` is
automatically injected into `AgentRuntime` — no extra configuration needed.
Internal components create typed loggers and log under their own category:

```csharp
// Any app with a DI container (ASP.NET, WPF, console, etc.)
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
```

Or manual construction (no DI container):

```csharp
var tools = new ToolRegistry();
tools.Register(new WeatherTool(...));

var runtime = new AgentRuntime(
    new MyLlmClient(...),
    tools,
    new[] { new RateLimitPolicy() },
    new[] { new CachedResultStrategy() },
    new DefaultPlanner(new MyLlmClient(...)));
```

To enable diagnostic logging in manual construction, pass an `ILoggerFactory`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddDebug();          // Visual Studio Output window
    builder.SetMinimumLevel(LogLevel.Warning);
});

var runtime = new AgentRuntime(
    new MyLlmClient(...),
    tools,
    loggerFactory: loggerFactory);
```

The `loggerFactory` parameter enables typed logging inside internal
components (e.g. LLM call strategies). `AgentRuntime` and each component
create their own `ILogger<T>` from the factory, so log entries identify
the originating class. When omitted, the runtime works without logging.

---

## Advanced: Custom Context

You can pass parameters and configure the runtime per request:

```csharp
var request = new AgentRequest(
    Goal: "Research the API base URL for provider XYZ",
    AllowedToolNames: ["search_web", "fetch_url"],
    Parameters: new Dictionary<string, object>
    {
        ["system_prompt"] = "You are a URL research specialist.",
        ["max_turns"] = 10,
        ["temperature"] = 0.0f,
        ["model"] = "gpt-4o",
        ["completion_timeout_seconds"] = 120,
        ["tool_timeout_seconds"] = 30,
        ["tool_max_retries"] = 2,
        ["max_quality_retries"] = 1
    });
```

---

## Models Reference

| Type | Properties |
|------|-----------|
| `AgentRequest` | `Goal`, `AllowedToolNames` (`null` = all tools, empty = no tools), `Parameters`, `AgentName`, `CapabilityRequirements` |
| `AgentResult` | `Success`, `Output`, `Reasoning`, `Steps`, `Usage`, `Metadata` |
| `DecisionResult` | `Decision`, `Approved`, `Confidence`, `Reasoning` |
| `PolicyResult` | `Applied`, `Score`, `Recommendation`, `Reasoning` |
| `StrategyResult` | `Success`, `Output`, `Reasoning`, `Artifacts` |
| `PlannedStep` | `Name`, `Type`, `Description`, `Parameters` |
| `ToolResult` | `Success`, `Output`, `Error` |
| `ToolInvocation` | `Name`, `Arguments`, `InvocationId` |
| `ToolDefinition` | `Name`, `Description`, `Parameters` (JSON schema), metadata (`Category`, `Version`, `DefaultTimeout`, etc.) |
| `ToolExecutionPolicy` | `MaxRetries`, `Timeout`, `LogEnabled`, `MetricsEnabled` |
| `QualityGateResult` | `Approved`, `Retry`, `Reason`, `ReplacementResult` |
| `ValidationResult` | `IsValid`, `Error` |
| `LlmMessage` | `Role`, `Content`, `ToolCalls`, `ToolCallId` |
| `LlmResponse` | `Content`, `ToolCalls`, `Usage` |
| `LlmTokenUsage` | `PromptTokens`, `CompletionTokens` |
| `ExecutionStatus` | `Created`, `Validating`, `Planning`, `Executing`, `Completed`, `Failed`, `Cancelled`, ... |
| `AgentExecutionState` | `ExecutionId`, `Status`, `Metadata`, `State`, `Items`, `Artifacts` |
| `ExecutionEvent` | `ExecutionId`, `EventType`, `Timestamp`, `Data` |
| `ExecutionManifest` | `ExecutionId`, `Status`, `Duration`, `Events`, `Artifacts` |
| `ExecutionSnapshot` | `SchemaVersion`, `ExecutionId`, `Status`, `Goal`, counters, result |
| `ExecutionMetrics` | `TotalExecutions`, `SuccessRate`, `P50/P95/P99Duration`, LLM/tool metrics |
| `DiagnosticReport` | `Entries`, `Categories`, severity levels |
| `ExecutionGraph` | `Nodes`, `Edges`, `ToMermaid()` export |
| `CapabilityProfile` | `ProviderName`, `Capabilities`, `Limits` |
| `ResourceEstimate` / `ResourceUsage` / `ResourceLimits` | Cost, token, time, and tool-call budgets |
| `WorkflowDefinition` / `WorkflowNode` / `WorkflowResult` | DAG-based workflow models |
| `AgentEvent` (and subtypes) | Streaming events: `ModelChunkEvent`, `ToolCompletedEvent`, etc. |
| `CompletedToolCall` | `Id`, `Name`, `Arguments` — flushed from streaming buffer |
| `StreamingToolCallUpdate` | `ToolCallId`, `FunctionName`, `ArgumentsChunk` — partial streaming input |
| `InputValidationResult` | `IsValid`, `Error` — input validator result |
| `DangerLevel` | `Safe`, `Low`, `Medium`, `High`, `Critical` |
| `ToolInputScope` | `AllowedPaths`, `AllowedHosts`, `MaxInputSizeBytes`, `AllowWrites`, etc. |

---

## Streaming

`IStreamingAgentRuntime` provides real-time execution events via `IAsyncEnumerable<AgentEvent>`:

```csharp
var runtime = provider.GetRequiredService<IStreamingAgentRuntime>();

await foreach (var evt in runtime.RunStreamingAsync(request))
{
    switch (evt)
    {
        case ModelChunkEvent chunk:
            Console.Write(chunk.Content);
            break;
        case ToolCompletedEvent tool:
            Console.WriteLine($"[Tool] {tool.ToolName}: {tool.Output}");
            break;
        case ExecutionCompletedEvent done:
            Console.WriteLine($"\nDone: {done.Result.Output}");
            break;
    }
}
```

---

## Memory Tiers

AiCleverness supports three memory tiers behind `IAggregateMemory`:

| Tier | Interface | Purpose |
|------|-----------|--------|
| Working | `IWorkingMemory` | Per-execution ephemeral state |
| Long-term | `ILongTermMemory` | Persistent cross-execution storage |
| Vector | `IVectorMemory` | Semantic search with embeddings |

```csharp
services.AddAiClevernessRuntime();  // includes InMemoryAgentMemory
// Or register individual tiers:
services.AddWorkingMemory<RedisWorkingMemory>();
services.AddLongTermMemory<SqlLongTermMemory>();
services.AddVectorMemory<PgVectorMemory>();
```

---

## Security and Approval

| Interface | Purpose |
|-----------|--------|
| `IPromptGuard` | Validate input prompts (injection, jailbreak, PII) |
| `IToolCallValidator` | Validate tool calls before execution |
| `IOutputGuard` | Validate output (secret leakage, unsafe content) |
| `IApprovalService` | Human-in-the-loop pause/approve/reject/resume |
| `IScopeValidator` | Enforce tool input scope isolation |
| `IAgentInputValidator` | Validate agent input before execution (per-agent scoped) |
| `IIdempotencyCache` | Prevent duplicate execution of successful tool calls during retries |

Tools can declare `RequiresApproval = true` and `DangerLevel` in their `ToolDefinition`. The runtime respects these via the approval service and danger-level validation.

---

## Workflows and Multi-Agent

```csharp
// Sequential workflow
var workflow = new WorkflowDefinition(
    Name: "research-pipeline",
    Nodes: [
        new WorkflowNode("search", "tool-execution", new() { ["tool"] = "search_web" }),
        new WorkflowNode("analyze", "agent-execution", new() { ["goal"] = "Analyze results" }),
        new WorkflowNode("report", "agent-execution", new() { ["goal"] = "Write report" })
    ]);

services.AddWorkflowExecutor<SequentialWorkflowExecutor>();

// Router agent
services.AddRouterAgent<MyRouterAgent>();
```

---

## Persistence, Replay, and Hosting

| Interface | Purpose |
|-----------|--------|
| `ICheckpointStore` | Persist execution checkpoints |
| `IExecutionJournal` | Append-only execution event journal |
| `IExecutionReplayer` | Replay executions from checkpoints |
| `IExecutionScheduler` | Queue, prioritize, and schedule executions |
| `IShutdownCoordinator` | Graceful shutdown with drain |

```csharp
services.AddInMemoryCheckpointStore();
services.AddInMemoryExecutionJournal();
services.AddHostedAgentRuntime(options =>
{
    options.MaxConcurrentExecutions = 4;
    options.GracePeriodSeconds = 30;
});
```

---

## Observability and Diagnostics

| Interface | Purpose |
|-----------|--------|
| `IMetricsCollector` | Structured metrics (P50/P95/P99 durations, token usage) |
| `IDiagnosticCollector` | Diagnostic traces for decisions |
| `IStartupAnalyzer` | Validate DI configuration at startup |

```csharp
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();  // sample OTel observer
```

Execution graphs can be exported to Mermaid diagrams:

```csharp
var graph = ExecutionGraph.FromEvents(executionId, status, duration, events);
var mermaid = graph.ToMermaid();
```

---

## Agent Scoping — Per-Agent Extension Points

Every extension point supports two registration modes:

```csharp
// GLOBAL — runs on ALL agents (default, backward compatible)
services.AddAgentQualityGate<JsonQualityGate>();
services.AddAgentResultValidator<MyValidator>();

// AGENT-SCOPED — runs only on agents matching the predicate
services.AddAgentQualityGate<UrlStructureGate>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
services.AddAgentInputValidator<PricingFormatValidator>(
    appliesTo: ctx => ctx.AgentName == "PricingAgent");
services.AddAgentResultValidator<DomainValidator>(
    appliesTo: ctx => ctx.AgentName == "DataAgent");
```

Pass `AgentName` in the request:

```csharp
var request = new AgentRequest(
    Goal: "Find pricing URL",
    AgentName: "UrlResearchAgent",      // matches scoping predicates
    AllowedToolNames: ["search_web"]);
```

### Input Validation

A dedicated pipeline stage validates input before execution begins:

```csharp
services.AddAgentInputValidator<ValidUrlInputValidator>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
```

Input validators run after policies, before planning. They short-circuit execution on failure.

---

## Streaming Tool Buffer

When an LLM streams tool calls as partial JSON chunks, the `ToolCallBuffer` accumulates them into complete invocations:

```csharp
var buffer = new ToolCallBuffer();

// Feed streaming chunks
buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"q\":")]);
buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"AI\"}")]);

// Flush completed tool calls (balanced JSON detected)
var ready = buffer.FlushCompleted();
// ready[0] = CompletedToolCall("call-1", "search", "{\"q\":\"AI\"}")
```

The buffer handles nested JSON, strings with braces, escaped quotes, multiple concurrent tool calls, and array arguments.

---

## Tool Idempotency

Prevents duplicate execution of side-effecting tools during quality-gate retries:

```csharp
services.AddIdempotencyCache();

// Wrap the tool executor with idempotency
var idempotentExecutor = new IdempotentToolExecutor(
    inner: defaultExecutor,
    cache: cache,
    executionScope: executionId);
```

Key behaviors:
- **Cache on success only** — failed calls always retry
- **Per-execution scope** — no cross-execution cache pollution
- **Explicit InvocationId** takes priority over semantic key
- **Semantic fallback** — tool name + SHA256(sorted arguments) when no InvocationId

---

## Project Structure

```
AICleverness/
├── AiClevernessLib/
│   ├── Abstractions/          # Public interfaces (IAgentRuntime, ITool, ILlmClient, etc.)
│   ├── Models/                # Records and DTOs (AgentRequest, AgentResult, etc.)
│   ├── Runtime/               # Default implementations (sealed classes)
│   └── DependencyInjection/   # DI extension methods
├── AiClevernessLib.Demo/      # Demo console app — all features demonstrated
├── AiClevernessLib.Tests/     # Unit tests (xUnit + FluentAssertions)
└── docs/                      # Developer manual source (MkDocs Material)
```

---

## Dependencies

- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions` (opt-in via `AddHostedAgentRuntime`)
- `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` (opt-in)

Zero external AI provider SDKs — bring your own.

## Documentation

- **[Developer manual](https://alexnek.github.io/AICleverness/)** — the full guide: installation, quick start, DI setup, runtime pipeline, tools, streaming, memory, security, workflows, observability, and API reference.
- **[API reference](https://alexnek.github.io/AICleverness/api-reference/interfaces/)** — every interface, model, and DI extension.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

MIT
