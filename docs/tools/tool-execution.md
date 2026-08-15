# Tool Execution

A tool only does its own work. Everything around the call — the timeout,
the retries, the checks — belongs to the `IToolExecutor`. This keeps your
tools simple.

## Default Executor

`AddAiClevernessRuntime()` registers the default tool executor. You can
change its behavior for one run with request parameters:

```csharp
["tool_timeout_seconds"] = 30,   // stop a tool call after 30 seconds
["tool_max_retries"] = 2         // repeat a failed tool call up to 2 times
```

If you set nothing, the runtime uses its global defaults (for example
`DefaultToolMaxRetries` from the runtime options). A single tool can also
set its own timeout with `ToolDefinition.DefaultTimeout`.

## Custom Executor

You can replace the whole executor with your own class:

```csharp
services.AddAgentToolExecutor<MyToolExecutor>();
```

A custom executor can add its own behavior around every tool call — for
example: stop calling a service that keeps failing (circuit breaking),
write an audit log, or count costs. The tools themselves stay unchanged.

## Validation Before Execution

Two checks run before a tool call executes:

- `IToolCallValidator` — checks the tool call itself: are the arguments
  correct, is the tool allowed, is the danger level acceptable?
- `IScopeValidator` — checks what the tool may touch. The scope
  (`ToolInputScope`) defines the allowed file paths, the allowed hosts, the
  maximum input size, and whether writing is allowed.

See [Security and Approval](../security/security-approval.md) for how they
work together.

## Protection Against Duplicate Calls

Some tools have real side effects — sending mail, creating records. During
a retry, the model may ask for the same tool call again. To prevent
running it twice, see [Tool Idempotency](tool-idempotency.md).
