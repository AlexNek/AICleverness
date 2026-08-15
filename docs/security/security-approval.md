# Security and Approval

Security checks do not sit in one place. They are spread over the whole
pipeline:

- **Before the run** — guards check the input prompt.
- **Before every tool call** — validators check the call.
- **During a tool call** — a human approves it, if the tool requires it.
- **After the run** — guards check the output.

## The Security Interfaces

| Interface | What it does |
| --- | --- |
| `IPromptGuard` | Checks incoming prompts: injection attacks, jailbreak attempts, private data |
| `IToolCallValidator` | Checks a tool call before it runs |
| `IOutputGuard` | Checks the output: leaked secrets, dangerous content |
| `IApprovalService` | Pauses the run so a human can approve or reject it, then continues |
| `IScopeValidator` | Limits what a tool can touch (paths, hosts, size, writes) |
| `IAgentInputValidator` | Checks the agent input before the run (can be registered for one agent only) |
| `IIdempotencyCache` | Stops the same tool call from running twice during retries |

## Approval Flow

A tool can say two things about itself in its `ToolDefinition`:

- `DangerLevel` (`Safe`, `Low`, `Medium`, `High`, `Critical`) — the runtime
  uses this for its danger-level checks.
- `RequiresApproval = true` — the runtime sends the call to the
  `IApprovalService` first. The service pauses the run, waits for a human
  decision, and then continues (approved) or stops (rejected).

## Tool Input Scopes

`IScopeValidator` checks a `ToolInputScope` for each tool:

- `AllowedPaths` / `AllowedHosts` — what the tool may reach
- `MaxInputSizeBytes` — how big the input may be
- `AllowWrites` — whether the tool may write anything

These limits apply no matter what the model asks for. See
[Tool Execution](../tools/tool-execution.md) for where these checks happen.
