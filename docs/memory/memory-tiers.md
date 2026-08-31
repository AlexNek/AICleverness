# Memory Tiers

Agents can store and read data through memory. AiCleverness has three
types of memory. `IAggregateMemory` gives you one entry point to all three:

| Type | Interface | What it is for |
| --- | --- | --- |
| Working | `IWorkingMemory` | Temporary data for one run; gone when the run ends |
| Long-term | `ILongTermMemory` | Data that stays, also between different runs |
| Vector | `IVectorMemory` | Search by meaning (embeddings), not by exact words |

## Registration

`AddAiClevernessRuntime()` does not register `IAgentMemory` as a shared DI
service. The runtime creates a fresh memory instance for each execution.
This is intentional: a singleton could leak state between runs, and the old
singleton was not consumed by `AgentRuntime` or capable of replacing its
per-execution memory. Applications needing persistent or custom memory must
wire it through an explicit execution integration.

## Simple Key-Value Memory

`IAgentMemory` is the simplest form: a key-value store that an agent can
use during a run. The built-in runtime memory is isolated to that execution;
application-owned integrations can provide Redis, SQLite, or another
implementation when persistence is required:

```csharp
public class RedisAgentMemory : IAgentMemory
{
    public async Task SaveAsync<T>(string key, T value, CancellationToken ct) { ... }
    public async Task<T?> LoadAsync<T>(string key, CancellationToken ct) { ... }
    public async Task<bool> ContainsAsync(string key, CancellationToken ct) { ... }
    public async Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct) { ... }
}
```
