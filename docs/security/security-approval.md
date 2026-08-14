# Security and Approval

Security checks are distributed across the pipeline — input guards before
execution, tool-call validation before every invocation, output guards after,
and human approval in the middle when a tool demands it.

## The Security Interfaces

| Interface | Purpose |
| --- | --- |
| `IPromptGuard` | Validate input prompts (injection, jailbreak, PII) |
| `IToolCallValidator` | Validate tool calls before execution |
| `IOutputGuard` | Validate output (secret leakage, unsafe content) |
| `IApprovalService` | Human-in-the-loop pause/approve/reject/resume |
| `IScopeValidator` | Enforce tool input scope isolation |
| `IAgentInputValidator` | Validate agent input before execution (per-agent scoped) |
| `IIdempotencyCache` | Prevent duplicate tool execution during retries |

## Approval Flow

Tools can declare `RequiresApproval = true` and a `DangerLevel` in their
`ToolDefinition`. The runtime respects both:

- `DangerLevel` (`Safe`, `Low`, `Medium`, `High`, `Critical`) drives
  danger-level validation
- `RequiresApproval` routes the invocation through `IApprovalService`,
  which can pause the execution, wait for a human decision, then resume or
  reject

## Tool Input Scopes

`IScopeValidator` enforces `ToolInputScope` constraints per tool:

- `AllowedPaths` / `AllowedHosts`
- `MaxInputSizeBytes`
- `AllowWrites`

This isolates what each tool may touch, independent of what the LLM asks
for. See [Tool Execution](../tools/tool-execution.md) for where these
checks sit in the executor boundary.
