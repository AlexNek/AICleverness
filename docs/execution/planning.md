# Planning

Planning is optional. If you register a planner, the runtime first splits
the goal into steps, and then starts the work.

A plan is a list of `PlannedStep` records. Each step has a `Name`, a
`Type`, a `Description`, and `Parameters`.

## Built-in Planners

```csharp
services.AddDefaultPlanner();     // asks the LLM to build the plan
services.AddSequentialPlanner();  // a fixed list of steps, no LLM needed
```

## Named Planners

You can register several planners and choose one for each request:

```csharp
services.AddNamedPlanner<CustomPlanner>();
```

The `IPlannerRegistry` finds the right planner by name when the run starts.
A planner that identifies itself with a name implements
`INamedAgentPlanner`.

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
        // Split request.Goal into steps
        ...
    }
}
```

If no planner is registered, the runtime skips this step completely and
goes directly to the strategies and the LLM tool loop.
