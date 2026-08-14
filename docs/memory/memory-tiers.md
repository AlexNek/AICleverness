# Memory Tiers

AiCleverness supports three memory tiers behind the aggregate interface
`IAggregateMemory`:

| Tier | Interface | Purpose |
| --- | --- | --- |
| Working | `IWorkingMemory` | Per-execution ephemeral state |
| Long-term | `ILongTermMemory` | Persistent cross-execution storage |
| Vector | `IVectorMemory` | Semantic search with embeddings |

## Registration

`AddAiClevernessRuntime()` includes an in-memory default
(`InMemoryAgentMemory`). Register individual tiers to swap backends:

```csharp
services.AddWorkingMemory<RedisWorkingMemory>();
services.AddLongTermMemory<SqlLongTermMemory>();
services.AddVectorMemory<PgVectorMemory>();
```

## Simple Key-Value Memory

`IAgentMemory` is the flat key-value storage available to agents during
execution. Default is in-memory; swap for Redis, SQLite, etc.:

```csharp
public class RedisAgentMemory : IAgentMemory
{
    public async Task SaveAsync<T>(string key, T value, CancellationToken ct) { ... }
    public async Task<T?> LoadAsync<T>(string key, CancellationToken ct) { ... }
    public async Task<bool> ContainsAsync(string key, CancellationToken ct) { ... }
    public async Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken ct) { ... }
}
```
