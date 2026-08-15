# Observability and Diagnostics

How do you know what your agents are doing? Three collectors give you the
answers:

| Interface | What it gives you |
| --- | --- |
| `IMetricsCollector` | Numbers: run durations (P50/P95/P99), token usage, success rate |
| `IDiagnosticCollector` | Detailed traces that explain decisions |
| `IStartupAnalyzer` | Checks your DI setup when the application starts |

```csharp
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();  // an example observer for OpenTelemetry
```

## Metrics

`ExecutionMetrics` collects the statistics of your runs: `TotalExecutions`,
`SuccessRate`, `P50/P95/P99Duration`, and numbers about LLM calls and tool
calls.

## Diagnostics

`DiagnosticReport` collects entries grouped by category and severity. Use
it to understand **why** something happened — for example, why a policy
stopped a run, or why a gate asked for a retry.

## Lifecycle Observers

`IAgentObserver` gets a message on every lifecycle event (run started,
finished, failed). Write your own observer to send these messages to any
monitoring system:

```csharp
services.AddAgentObserver<RuntimeObserver>();
```

## Execution Graphs

You can export the steps of a run as a Mermaid diagram:

```csharp
var graph = ExecutionGraph.FromEvents(executionId, status, duration, events);
var mermaid = graph.ToMermaid();
```

For complete records of one run — all events, artifacts, counters, and the
duration — use `ExecutionManifest` and `ExecutionSnapshot`. They are meant
for auditing and dashboards.
