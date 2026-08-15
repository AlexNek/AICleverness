# Defining Tools

A tool performs work — it never decides anything. The model decides *what*
to do; the tool only does what the model asks for, when the runtime calls
it.

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

The `Definition` contains a JSON schema that describes the tool's
arguments. The model reads this schema to know how to call the tool.

When the runtime calls your tool, it passes a `ToolInvocation` with the
`Name`, the `Arguments`, and an optional `InvocationId`. Your tool returns
a `ToolResult` with `Success`, `Output`, and `Error`.

## Tool Metadata

A `ToolDefinition` can carry more information than name, description, and
schema. The runtime and other components use this metadata:

| Property | What it is for |
| --- | --- |
| `Category` | Group tools together, for example in a UI |
| `Version` | The version of the tool |
| `CostPerCall` | Estimate the cost and budget of a run |
| `RequiresApproval` | If true, the call first goes through the [approval service](../security/security-approval.md) |
| `DefaultTimeout` | The timeout for this tool, used by the executor |
| `Parallelizable` | Whether two calls of this tool can run at the same time |
| `DangerLevel` | `Safe`, `Low`, `Medium`, `High`, `Critical` |
| `Authentication` | What credentials the tool needs |
| `Tags` | Any labels you want to add |

The runtime checks `RequiresApproval` and `DangerLevel` before a tool call
runs.

## Registration

```csharp
services.AddAgentTool<WeatherTool>();
services.AddAgentTool<SearchTool>();
```

Or register a tool directly on the registry:

```csharp
registry.Register(new WeatherTool(...));
```

A registered tool is not automatically used. For one run, only the tools
listed in `AgentRequest.AllowedToolNames` are offered to the model — see
[Runtime Pipeline](../concepts/runtime-pipeline.md).
