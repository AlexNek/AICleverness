# Persistence, Replay, and Hosting

Execution state can be persisted, replayed, and scheduled — all opt-in.

## The Persistence Interfaces

| Interface | Purpose |
| --- | --- |
| `ICheckpointStore` | Persist execution checkpoints |
| `IExecutionJournal` | Append-only execution event journal |
| `IExecutionReplayer` | Replay executions from checkpoints |
| `IExecutionScheduler` | Queue, prioritize, and schedule executions |
| `IShutdownCoordinator` | Graceful shutdown with drain |

## Registration

```csharp
services.AddInMemoryCheckpointStore();
services.AddInMemoryExecutionJournal();
services.AddHostedAgentRuntime(options =>
{
    options.MaxConcurrentExecutions = 4;
    options.GracePeriodSeconds = 30;
});
```

The in-memory implementations are suitable for development and testing;
implement the interfaces for durable stores.

## Hosting

`AddHostedAgentRuntime()` registers `HostedAgentRuntimeService`, an
`IHostedService` that:

- Limits concurrent executions (`MaxConcurrentExecutions`)
- Drains in-flight executions on shutdown (`GracePeriodSeconds`) via
  `IShutdownCoordinator`
- Integrates with the execution scheduler for queued work

Execution artifacts (`IExecutionArtifact` / `IExecutionArtifactCollection`)
and snapshots (`ExecutionSnapshot`) capture the state needed for replay and
auditing.
