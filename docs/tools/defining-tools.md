# Defining Tools

Tools are **executors, not deciders**. They never decide what to do — they
only perform work when called by the runtime.

## The ITool Interface

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolDefinition Definition { get; }  // JSON schema for the LLM
    Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct);
}
```

The `Definition` exposes a JSON schema describing the tool's arguments, so
the LLM knows how to call it. `ToolInvocation` carries the `Name`,
`Arguments`, and an optional `InvocationId`; the tool returns a `ToolResult`
with `Success`, `Output`, and `Error`.

## Tool Metadata

`ToolDefinition` supports metadata beyond name, description, and schema:

| Property | Purpose |
| --- | --- |
| `Category` | Grouping for registries and UI |
| `Version` | Tool versioning |
| `CostPerCall` | Budgeting and resource estimates |
| `RequiresApproval` | Route through the [approval service](../security/security-approval.md) |
| `DefaultTimeout` | Per-tool timeout for the executor |
| `Parallelizable` | Whether concurrent calls are safe |
| `DangerLevel` | `Safe`, `Low`, `Medium`, `High`, `Critical` |
| `Authentication` | Credential requirements |
| `Tags` | Free-form labels |

The runtime respects `RequiresApproval` and `DangerLevel` through the
approval service and danger-level validation.

## Registration

```csharp
services.AddAgentTool<WeatherTool>();
services.AddAgentTool<SearchTool>();
```

Or manually against the registry:

```csharp
registry.Register(new WeatherTool(...));
```

Only tools listed in `AgentRequest.AllowedToolNames` are offered to the LLM
for a given run — see [Runtime Pipeline](../concepts/runtime-pipeline.md).
