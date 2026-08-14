# Observability and Diagnostics

| Interface | Purpose |
| --- | --- |
| `IMetricsCollector` | Structured metrics (P50/P95/P99 durations, token usage) |
| `IDiagnosticCollector` | Diagnostic traces for decisions |
| `IStartupAnalyzer` | Validate DI configuration at startup |

```csharp
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();  // sample OTel observer
```

## Metrics

`ExecutionMetrics` aggregates runtime statistics: `TotalExecutions`,
`SuccessRate`, `P50/P95/P99Duration`, plus LLM and tool metrics.

## Diagnostics

`DiagnosticReport` collects entries by category and severity — useful for
understanding why a policy blocked a run or a gate requested a retry.

## Lifecycle Observers

`IAgentObserver` receives lifecycle events for any custom telemetry
pipeline. Register one per backend:

```csharp
services.AddAgentObserver<RuntimeObserver>();
```

## Execution Graphs

Execution graphs can be exported to Mermaid diagrams:

```csharp
var graph = ExecutionGraph.FromEvents(executionId, status, duration, events);
var mermaid = graph.ToMermaid();
```

`ExecutionManifest` and `ExecutionSnapshot` provide full per-execution
records (events, artifacts, counters, duration) for auditing dashboards.
