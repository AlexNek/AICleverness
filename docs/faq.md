# FAQ

## Do I need a specific AI provider?

No. The library has no dependency on any AI provider SDK. You write your
own [ILlmClient](concepts/llm-client.md) — for OpenAI, Anthropic, Ollama,
Azure OpenAI, or a local model — and register it with
`AddAiClevernessLlmClient<T>()`.

## Do I have to use dependency injection?

No. The library works best with DI, but every part can also be created
directly with `new`. See the "Without DI" example in
[Dependency Injection](getting-started/dependency-injection.md).

## How do I answer known questions without calling the LLM?

Register an [IAgentStrategy](concepts/policies-strategies.md). Strategies
run before the LLM loop. When one can answer the goal, the runtime returns
its answer immediately — no tokens, no waiting time. The library already
contains a strategy that returns cached results.

## How do I stop a tool from running twice during a retry?

A retry after a quality gate may ask for the same tool call again. For
tools with real effects (sending mail, creating records) this is
dangerous. Wrap the executor with `IdempotentToolExecutor` and register the
idempotency cache — see [Tool Idempotency](tools/tool-idempotency.md).
Successful calls are remembered per run and their result is returned again
instead of running the tool twice.

## What is the difference between quality gates, validators, and output guards?

- **Quality gates** check the answer. They can ask the model to try again
  (with the reason), or they can replace the answer with their own.
- **Validators** are simple yes/no checks. If they fail, the run is marked
  as unsuccessful.
- **Output guards** are the security checks: leaked secrets, dangerous
  content.

## Can different agents have different rules?

Yes. Every component can be registered for
[one agent only](execution/agent-scoping.md). You give a small `appliesTo`
condition that is checked against `AgentRequest.AgentName`.

## How do I see what the runtime did?

`AgentResult.Steps` contains the log of the run. For more, use observers,
the [metrics and diagnostic collectors](operations/observability.md), or
export an `ExecutionGraph` as a Mermaid diagram.

## Which .NET versions are supported?

.NET 10.0 or later. See [Installation](getting-started/installation.md) for
the list of dependencies.
