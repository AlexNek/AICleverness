# Persistence, Replay, and Hosting

By default, a run lives only in memory. If you need more — saving the state
of a run, running it again later, or scheduling many runs — you can add it.
Nothing of this is active unless you register it.

## The Persistence Interfaces

| Interface | What it does |
| --- | --- |
| `ICheckpointStore` | Saves the state of a run, so it can continue later |
| `IExecutionJournal` | A log of all run events; entries are only added, never changed |
| `IExecutionReplayer` | Runs a saved execution again |
| `IExecutionScheduler` | Puts runs in a queue and decides their order |
| `IShutdownCoordinator` | Clean shutdown: let running work finish first |

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

The in-memory versions are good for development and testing. For
production, implement the same interfaces with a real database.

## Hosting

`AddHostedAgentRuntime()` registers `HostedAgentRuntimeService`. This is an
`IHostedService` (a standard .NET background service) that:

- Limits how many runs go at the same time (`MaxConcurrentExecutions`)
- On shutdown, waits for running work to finish before stopping
  (`GracePeriodSeconds`), through `IShutdownCoordinator`
- Works together with the execution scheduler for queued runs

Execution artifacts (`IExecutionArtifact` / `IExecutionArtifactCollection`)
and snapshots (`ExecutionSnapshot`) save the state of a run. You need them
for replay and for auditing.
