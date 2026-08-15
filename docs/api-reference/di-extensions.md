# DI Extensions

These methods connect the library's parts to DI (dependency injection).
They all live in `AiCleverness.DependencyInjection`.

## Core Runtime

| Method | Registers |
| --- | --- |
| `AddAiClevernessRuntime()` | The runtime, the default tool executor, the registries, and the in-memory memory. Optionally takes `AgentRuntimeOptions` |
| `AddAiClevernessLlmClient<T>()` | Your `ILlmClient` implementation |

The defaults you can set in `AgentRuntimeOptions`:

- `DefaultMaxTurns` — maximum number of LLM turns per run
- `DefaultCompletionTimeoutSeconds` — maximum wait time for one LLM call
- `DefaultMaxQualityRetries` — how often the model may retry after a gate rejects
- `DefaultToolMaxRetries` — how often a failed tool call is repeated

## Extension Points

| Method | Registers |
| --- | --- |
| `AddAgentPolicy<T>()` | `IAgentPolicy` |
| `AddAgentInputValidator<T>()` | `IAgentInputValidator` |
| `AddAgentStrategy<T>()` | `IAgentStrategy` |
| `AddAgentQualityGate<T>()` | `IAgentQualityGate` |
| `AddAgentResultValidator<T>()` | `IAgentResultValidator` |
| `AddAgentResultTransformer<T>()` | `IAgentResultTransformer` |
| `AddAgentObserver<T>()` | `IAgentObserver` |

Every method above also accepts an optional `appliesTo` condition, so it
runs for [one agent only](../execution/agent-scoping.md).

## Planning

| Method | Registers |
| --- | --- |
| `AddDefaultPlanner()` | A planner that asks the LLM to build the plan |
| `AddSequentialPlanner()` | A planner with a fixed list of steps (no LLM needed) |
| `AddNamedPlanner<T>()` | A planner with a name, selectable per request |

## Tools

| Method | Registers |
| --- | --- |
| `AddAgentTool<T>()` | An `ITool` in the tool registry |
| `AddAgentToolExecutor<T>()` | Your own `IToolExecutor` |
| `AddIdempotencyCache()` | An `IIdempotencyCache` (in-memory by default) |

## Memory

| Method | Registers |
| --- | --- |
| `AddWorkingMemory<T>()` | `IWorkingMemory` |
| `AddLongTermMemory<T>()` | `ILongTermMemory` |
| `AddVectorMemory<T>()` | `IVectorMemory` |

## Persistence and Hosting

| Method | Registers |
| --- | --- |
| `AddInMemoryCheckpointStore()` | An `ICheckpointStore` that lives in memory |
| `AddInMemoryExecutionJournal()` | An `IExecutionJournal` that lives in memory |
| `AddHostedAgentRuntime()` | `HostedAgentRuntimeService` with options for how many runs go at the same time and how long to wait on shutdown |

## Observability

| Method | Registers |
| --- | --- |
| `AddMetricsCollector()` | `IMetricsCollector` |
| `AddDiagnosticCollector()` | `IDiagnosticCollector` |
| `AddStartupAnalyzer()` | `IStartupAnalyzer` |
| `AddOpenTelemetryObserver()` | An example `IAgentObserver` that sends data to OpenTelemetry |

## Workflows and Routing

| Method | Registers |
| --- | --- |
| `AddWorkflowExecutor<T>()` | `IWorkflowExecutor` |
| `AddRouterAgent<T>()` | `IRouterAgent` |
