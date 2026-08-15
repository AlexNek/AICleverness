# Agent Scoping

You can have several agents in one application. By default, every
registered component (policy, gate, validator, ...) runs for **all**
agents. With scoping, you can say: "this component runs only for *this*
agent".

## Two Ways to Register

```csharp
// GLOBAL — runs on ALL agents (this is the default)
services.AddAgentQualityGate<JsonQualityGate>();
services.AddAgentResultValidator<MyValidator>();

// SCOPED — runs only on agents that match the condition
services.AddAgentQualityGate<UrlStructureGate>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
services.AddAgentInputValidator<PricingFormatValidator>(
    appliesTo: ctx => ctx.AgentName == "PricingAgent");
services.AddAgentResultValidator<DomainValidator>(
    appliesTo: ctx => ctx.AgentName == "DataAgent");
```

The `appliesTo` argument is a small function that returns `true` or
`false`. It receives the `IAgentContext`, so you can match on any context
property — not only the agent name.

## Selecting the Agent

The request decides which agent runs, through `AgentName`. The scoped
components compare their condition against this name:

```csharp
var request = new AgentRequest(
    Goal: "Find pricing URL",
    AgentName: "UrlResearchAgent",      // scoped components match against this
    AllowedToolNames: ["search_web"]);
```

## Input Validation

Input validation is its own step in the pipeline. It checks the input
before any work starts:

```csharp
services.AddAgentInputValidator<ValidUrlInputValidator>(
    appliesTo: ctx => ctx.AgentName == "UrlResearchAgent");
```

Input validators run after the policies and before the planning. If one
fails, the run stops immediately and returns an `InputValidationResult`
with `IsValid` and `Error`.

## Implementation Note

Scoping works with small wrapper classes (`FilteredPolicy`, `FilteredGate`,
and so on) in `Runtime/Filtering`. The runtime itself contains no scoping
rules.
