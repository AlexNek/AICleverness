# Memory Tiers

Agents can store and read data through memory. AiCleverness has three
types of memory. `IAggregateMemory` gives you one entry point to all three:

| Type | Interface | What it is for |
| --- | --- | --- |
| Working | `IWorkingMemory` | Temporary data for one run; gone when the run ends |
| Long-term | `ILongTermMemory` | Data that stays, also between different runs |
| Vector | `IVectorMemory` | Search by meaning (embeddings), not by exact words |

## Registration

`AddAiClevernessRuntime()` already registers an in-memory default
(`InMemoryAgentMemory`). If you want a real database or cache behind one
type, register your own implementation for that type:

```csharp
services.AddWorkingMemory<RedisWorkingMemory>();
services.AddLongTermMemory<SqlLongTermMemory>();
services.AddVectorMemory<PgVectorMemory>();
```

## Simple Key-Value Memory

`IAgentMemory` is the simplest form: a key-value store that an agent can
use during a run. The default lives in memory, but you can replace it with
Redis, SQLite, or anything else:

```csharp
public class RedisAgentMemory : IAgentMemory
{
    public async Task SaveAsync<T>(string key, T value, CancellationToken ct) { ... }
    public async Task<T?> LoadAsync<T>(string key, CancellationToken ct) { ... }
    public async Task<bool> ContainsAsync(string key, CancellationToken ct) { ... }
    public async Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct) { ... }
}
```
