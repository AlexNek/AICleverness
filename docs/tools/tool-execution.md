# Tool Execution

Tools stay focused on work. `IToolExecutor` owns the cross-cutting runtime
behavior around every invocation: timeouts, retries, and validation.

## Default Executor

`AddAiClevernessRuntime()` registers the default tool executor. Request
parameters tune it per run:

```csharp
["tool_timeout_seconds"] = 30,
["tool_max_retries"] = 2
```

Global defaults come from the runtime options
(`DefaultToolMaxRetries`) and per-tool defaults from
`ToolDefinition.DefaultTimeout`.

## Custom Executor

Replace the boundary entirely:

```csharp
services.AddAgentToolExecutor<MyToolExecutor>();
```

A custom executor can add circuit breaking, auditing, or cost accounting
around `ITool.InvokeAsync` without touching any tool implementation.

## Validation Before Execution

Two guards run around tool calls:

- `IToolCallValidator` — validates tool calls before execution (arguments,
  scope, danger level)
- `IScopeValidator` — enforces tool input scope isolation
  (`ToolInputScope`: allowed paths, allowed hosts, max input size, write
  permission)

See [Security and Approval](../security/security-approval.md) for how they
fit together.

## Idempotent Execution

Side-effecting tools can be protected against duplicate execution during
quality-gate retries by wrapping the executor — see
[Tool Idempotency](tool-idempotency.md).
