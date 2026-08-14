# Policies and Strategies

Two extension points shape what happens *before* the LLM is ever called:
policies decide whether a run may happen at all, strategies answer the goal
without the LLM when they can.

## IAgentPolicy — Rules and Guardrails

Policies evaluate context **before** execution. They can block the run
entirely. Higher `Priority` values are evaluated first.

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

`PolicyResult` carries `Applied`, `Score`, `Recommendation`, and
`Reasoning`, so observers and diagnostics can record why a run was blocked.

## IAgentStrategy — Deterministic Shortcuts

Strategies bypass the LLM for known scenarios. If a strategy succeeds, the
runtime returns immediately without calling the LLM — no tokens, no latency.

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

`StrategyResult` carries `Success`, `Output`, `Reasoning`, and `Artifacts`.
The library ships ready-made strategies (`CachedResult`, rule-based) in the
`Runtime/Strategies` folder; both registration modes — global and
[agent-scoped](../execution/agent-scoping.md) — are supported.
