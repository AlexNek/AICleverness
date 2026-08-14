# Planning

Planning is optional. If a planner is registered, the runtime asks it to
decompose the goal into steps before execution begins.

## Built-in Planners

```csharp
services.AddDefaultPlanner();     // uses the LLM to produce the plan
services.AddSequentialPlanner();  // deterministic sequential plan
```

Plans are lists of `PlannedStep` records: `Name`, `Type`, `Description`, and
`Parameters`.

## Named Planners

Register multiple planners and select one per request through the planner
registry:

```csharp
services.AddNamedPlanner<CustomPlanner>();
```

`IPlannerRegistry` resolves the planner by name at execution time;
`INamedAgentPlanner` is the interface for planners that identify themselves.

## Custom Planners

Implement `IAgentPlanner`:

```csharp
public sealed class CustomPlanner : IAgentPlanner
{
    public Task<IReadOnlyList<PlannedStep>> PlanAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        // Decompose request.Goal into steps
        ...
    }
}
```

If no planner is registered, the runtime skips the stage and proceeds
directly to strategies and the LLM tool loop.
