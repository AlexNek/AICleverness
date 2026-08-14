# Agent Scoping

Every extension point supports two registration modes: **global** (runs on
all agents, the default) and **agent-scoped** (runs only on agents matching
a predicate).

## Registration Modes

```csharp
// GLOBAL — runs on ALL agents (default, backward compatible)
services.AddAgentQualityGate<JsonQualityGate>();
services.AddAgentResultValidator<MyValidator>();

// AGENT-SCOPED — runs only on agents matching the predicate
services.AddAgentQualityGate<UrlStructureGate>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
services.AddAgentInputValidator<PricingFormatValidator>(
    appliesTo: ctx => ctx.AgentName == "PricingAgent");
services.AddAgentResultValidator<DomainValidator>(
    appliesTo: ctx => ctx.AgentName == "DataAgent");
```

The `appliesTo` predicate receives the `IAgentContext`, so scoping can match
on any context property, not just the agent name.

## Selecting the Agent

Pass `AgentName` in the request — it drives the scoping predicates:

```csharp
var request = new AgentRequest(
    Goal: "Find pricing URL",
    AgentName: "UrlResearchAgent",      // matches scoping predicates
    AllowedToolNames: ["search_web"]);
```

## Input Validation

A dedicated pipeline stage validates input before execution begins:

```csharp
services.AddAgentInputValidator<ValidUrlInputValidator>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
```

Input validators run after policies, before planning. They short-circuit
execution on failure and return an `InputValidationResult`
(`IsValid`, `Error`).

## Implementation Note

Scoping is implemented with filter wrappers (`FilteredPolicy`,
`FilteredGate`, etc.) in `Runtime/Filtering` — the runtime itself stays
agnostic of scoping rules.
