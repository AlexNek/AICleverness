# Workflows and Multi-Agent

For pipelines that go beyond a single goal-and-tools run, AiCleverness
provides DAG-based workflow definitions and a router agent for dispatch.

## Defining a Workflow

```csharp
// Sequential workflow
var workflow = new WorkflowDefinition(
    Name: "research-pipeline",
    Nodes: [
        new WorkflowNode("search", "tool-execution", new() { ["tool"] = "search_web" }),
        new WorkflowNode("analyze", "agent-execution", new() { ["goal"] = "Analyze results" }),
        new WorkflowNode("report", "agent-execution", new() { ["goal"] = "Write report" })
    ]);
```

Nodes carry a name, a type (`tool-execution` runs a registered tool,
`agent-execution` runs a nested agent goal), and parameters. `WorkflowResult`
collects the per-node outputs.

## Execution

```csharp
services.AddWorkflowExecutor<SequentialWorkflowExecutor>();
```

`IWorkflowExecutor` is the abstraction; the library ships a sequential
executor, and the node graph can express DAG dependencies for custom
executors.

## Router Agent

Dispatch to specialized agents from a single entry point:

```csharp
services.AddRouterAgent<MyRouterAgent>();
```

Implement `IRouterAgent` to inspect the request and route it to the
appropriate named agent — combined with
[agent scoping](../execution/agent-scoping.md), each routed agent keeps its
own policies, gates, and validators.
