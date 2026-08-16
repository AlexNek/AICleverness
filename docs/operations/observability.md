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

## Model Failover Events

When [model failover](../execution/model-failover.md) is enabled, additional
observer methods fire:

- `OnLlmCallCompletedAsync(LlmCallInfo info)` — fires exactly once per LLM
  call attempt (success, error, or timeout). Provides full context: model
  name, turn, attempt number, duration, token usage, and failure classification.
- `OnModelSwitchedAsync(from, to, reason)` — fires when the runtime switches
  from one model to another due to a transient failure.

Streaming and bus counterparts:

- `ModelSwitchedAgentEvent` — emitted via the streaming event channel.
- `ModelSwitchedBusEvent` — published via the execution event bus.

Example observer implementation:

```csharp
public sealed class FailoverLoggingObserver : IAgentObserver
{
    public Task OnLlmCallCompletedAsync(LlmCallInfo info, CancellationToken ct)
    {
        Log.Information("LLM call: model={Model}, success={Success}, duration={Duration}ms",
            info.Model, info.Success, info.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task OnModelSwitchedAsync(
        string from, string to, string reason, CancellationToken ct)
    {
        Log.Warning("Model switched: {From} → {To}, reason: {Reason}", from, to, reason);
        return Task.CompletedTask;
    }

    // Other IAgentObserver methods use default no-op implementations.
}
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
