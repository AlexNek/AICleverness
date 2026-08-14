# FAQ

## Do I need a specific AI provider?

No. AiCleverness has zero provider SDK dependencies. Implement
[ILlmClient](concepts/llm-client.md) against any backend — OpenAI,
Anthropic, Ollama, Azure OpenAI, or a local model — and register it with
`AddAiClevernessLlmClient<T>()`.

## Is dependency injection mandatory?

No. The library is DI-first but every runtime piece is directly
constructible — see [Dependency Injection](getting-started/dependency-injection.md)
for the non-DI example.

## How do I answer known questions without calling the LLM?

Register an [IAgentStrategy](concepts/policies-strategies.md). Strategies
run before the LLM loop; when one succeeds the runtime returns immediately —
no tokens, no latency. A cached-result strategy ships with the library.

## How do I prevent duplicate side effects on retries?

Quality-gate retries can re-issue the same tool call. Wrap the executor with
`IdempotentToolExecutor` and register the idempotency cache — see
[Tool Idempotency](tools/tool-idempotency.md). Successful calls are cached
per execution and replayed instead of running twice.

## What is the difference between quality gates, validators, and output guards?

- **Quality gates** evaluate the result and can request an LLM retry with
  feedback, or substitute a replacement result
- **Validators** are simple pass/fail checks that mark the run unsuccessful
- **Output guards** are the security boundary (secret leakage, unsafe
  content)

## Can different agents have different rules?

Yes. Every extension point supports
[agent-scoped registration](execution/agent-scoping.md) with an `appliesTo`
predicate, matched against `AgentRequest.AgentName`.

## How do I see what the runtime did?

`AgentResult.Steps` carries the execution log. For structured telemetry use
observers, the [metrics and diagnostic collectors](operations/observability.md),
or export an `ExecutionGraph` to Mermaid.

## Which .NET versions are supported?

.NET 10.0 or later. See [Installation](getting-started/installation.md) for
the dependency list.
