# Workflows and Multi-Agent

A normal run is one goal plus tools. Sometimes you need more: several steps
after each other, or several specialized agents. For this, AiCleverness has
workflows and a router agent.

## Defining a Workflow

A workflow is a list of connected steps (nodes):

```csharp
// A workflow where one step runs after the other
var workflow = new WorkflowDefinition(
    Name: "research-pipeline",
    Nodes: [
        new WorkflowNode("search", "tool-execution", new() { ["tool"] = "search_web" }),
        new WorkflowNode("analyze", "agent-execution", new() { ["goal"] = "Analyze results" }),
        new WorkflowNode("report", "agent-execution", new() { ["goal"] = "Write report" })
    ]);
```

Each node has a name, a type, and parameters. There are two node types:

- `tool-execution` — the node runs one registered tool.
- `agent-execution` — the node runs a complete agent run with its own goal.

`WorkflowResult` collects the output of every node.

## Execution

```csharp
services.AddWorkflowExecutor<SequentialWorkflowExecutor>();
```

`IWorkflowExecutor` is the interface. The library contains a sequential
executor (one node after the other). If your nodes have dependencies
(node B needs the result of node A), you can express this in the node
graph and write your own executor.

## Router Agent

If you have several specialized agents, one router can decide which one
gets the request:

```csharp
services.AddRouterAgent<MyRouterAgent>();
```

Implement `IRouterAgent`: it looks at the request and sends it to the
right named agent. Together with
[agent scoping](../execution/agent-scoping.md), every agent keeps its own
policies, gates, and validators.
