# DI Extensions

All extension methods live in `AiCleverness.DependencyInjection`.

## Core Runtime

| Method | Registers |
| --- | --- |
| `AddAiClevernessRuntime()` | Runtime, default executor, registries, in-memory memory; optional `AgentRuntimeOptions` |
| `AddAiClevernessLlmClient<T>()` | Your `ILlmClient` implementation |

`AgentRuntimeOptions` defaults:

- `DefaultMaxTurns`
- `DefaultCompletionTimeoutSeconds`
- `DefaultMaxQualityRetries`
- `DefaultToolMaxRetries`

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

All of the above accept an optional `appliesTo` predicate for
[agent-scoped registration](../execution/agent-scoping.md).

## Planning

| Method | Registers |
| --- | --- |
| `AddDefaultPlanner()` | LLM-based planner |
| `AddSequentialPlanner()` | Deterministic sequential planner |
| `AddNamedPlanner<T>()` | Named planner via `IPlannerRegistry` |

## Tools

| Method | Registers |
| --- | --- |
| `AddAgentTool<T>()` | `ITool` into the registry |
| `AddAgentToolExecutor<T>()` | Custom `IToolExecutor` |
| `AddIdempotencyCache()` | `IIdempotencyCache` (in-memory default) |

## Memory

| Method | Registers |
| --- | --- |
| `AddWorkingMemory<T>()` | `IWorkingMemory` |
| `AddLongTermMemory<T>()` | `ILongTermMemory` |
| `AddVectorMemory<T>()` | `IVectorMemory` |

## Persistence and Hosting

| Method | Registers |
| --- | --- |
| `AddInMemoryCheckpointStore()` | `ICheckpointStore` |
| `AddInMemoryExecutionJournal()` | `IExecutionJournal` |
| `AddHostedAgentRuntime()` | `HostedAgentRuntimeService` with concurrency/grace options |

## Observability

| Method | Registers |
| --- | --- |
| `AddMetricsCollector()` | `IMetricsCollector` |
| `AddDiagnosticCollector()` | `IDiagnosticCollector` |
| `AddStartupAnalyzer()` | `IStartupAnalyzer` |
| `AddOpenTelemetryObserver()` | Sample OpenTelemetry `IAgentObserver` |

## Workflows and Routing

| Method | Registers |
| --- | --- |
| `AddWorkflowExecutor<T>()` | `IWorkflowExecutor` |
| `AddRouterAgent<T>()` | `IRouterAgent` |
