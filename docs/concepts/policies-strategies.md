# Policies and Strategies

Both run **before** the LLM is called, but they do different things:

- A **policy** is a guard. It decides if a run may happen at all. It can
  stop the run completely.
- A **strategy** is a shortcut. It answers the goal with plain code, so the
  LLM is not needed at all.

## IAgentPolicy — Rules and Guards

A policy looks at the request before the run starts. If it says "no", the
run stops immediately. When you register several policies, the one with the
higher `Priority` value runs first.

```csharp
public sealed class RateLimitPolicy : IAgentPolicy
{
    public string Name => "RateLimit";
    public int Priority => 100;    // higher = runs first
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

The policy returns a `PolicyResult` with four fields: `Applied` (did this
policy act?), `Score`, `Recommendation` (for example `"block"` or
`"allow"`), and `Reasoning` (the human-readable reason). Observers can log
these fields, so you can always see **why** a run was blocked.

## IAgentStrategy — Answer Without the LLM

A strategy handles a goal with plain code. If a strategy succeeds, the
runtime returns this answer immediately and never calls the LLM. This costs
no tokens and no waiting time.

A typical use is a cache: if the answer is already known, return it.

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

`CanExecute` decides if this strategy can handle the goal. If yes,
`ExecuteAsync` runs and returns a `StrategyResult` with `Success`, `Output`,
`Reasoning`, and `Artifacts`.

The library already contains ready-made strategies (`CachedResult` and a
rule-based one) in the `Runtime/Strategies` folder. You can register a
strategy for all agents, or for
[one agent only](../execution/agent-scoping.md).
