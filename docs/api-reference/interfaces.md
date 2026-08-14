# Core Interfaces

All public abstractions live in `AiCleverness.Abstractions`.

## Execution

| Interface | Purpose |
| --- | --- |
| `IAgentRuntime` | Orchestrates a run: `RunAsync(request, progress, cancellationToken)` |
| `IStreamingAgentRuntime` | Real-time events via `IAsyncEnumerable<AgentEvent>` |
| `IAgentContext` | Per-execution context (goal, agent name, parameters, memory) |
| `IAgentDecision` | Decision outcome abstraction |

## LLM and Tools

| Interface | Purpose |
| --- | --- |
| `ILlmClient` | Provider adapter — the only interface you must implement |
| `ITool` | Tool contract: name, description, JSON schema, `InvokeAsync` |
| `IToolRegistry` | Tool discovery and lookup |
| `IToolExecutor` | Execution boundary: timeout, retries, validation |
| `ICompensatingTool` | Tools that can undo a previous invocation |
| `IToolCallValidator` | Validate tool calls before execution |

## Pipeline Extension Points

| Interface | Purpose |
| --- | --- |
| `IAgentPolicy` | Pre-execution guardrails, can block runs |
| `IAgentInputValidator` | Input validation stage (per-agent scoped) |
| `IAgentPlanner` / `INamedAgentPlanner` | Goal decomposition into steps |
| `IPlannerRegistry` | Named planner resolution |
| `IAgentStrategy` / `IStrategyRegistry` | Deterministic shortcuts bypassing the LLM |
| `IAgentQualityGate` | Output evaluation with retry support |
| `IAgentResultValidator` | Simple pass/fail result checks |
| `IAgentResultTransformer` | Ordered final formatting/redaction |
| `IAgentObserver` | Lifecycle telemetry |
| `IAgentPipelineMiddleware` | Custom pipeline middleware |

## Memory

| Interface | Purpose |
| --- | --- |
| `IAgentMemory` | Flat key-value storage for agents |
| `IWorkingMemory` | Per-execution ephemeral state |
| `ILongTermMemory` | Persistent cross-execution storage |
| `IVectorMemory` | Semantic search with embeddings |
| `IAggregateMemory` | Aggregate facade over the three tiers |

## Security

| Interface | Purpose |
| --- | --- |
| `IPromptGuard` | Input prompt validation (injection, jailbreak, PII) |
| `IOutputGuard` | Output validation (secret leakage, unsafe content) |
| `IApprovalService` | Human-in-the-loop pause/approve/reject/resume |
| `IScopeValidator` | Tool input scope isolation |
| `IIdempotencyCache` | Duplicate tool execution prevention |

## Models, Capabilities, Prompts

| Interface | Purpose |
| --- | --- |
| `IModelCatalog` / `IModelManager` | Known model registry and lifecycle |
| `IModelSelectionPolicy` / `IModelSelectionStrategy` | Model selection rules |
| `ICapabilityResolver` | Provider capability resolution |
| `IPromptTemplate` / `IPromptRenderer` | Prompt templates and rendering |
| `IConversationManager` | Conversation state management |
| `ISummarizationStrategy` / `ITruncationStrategy` | Context window management |

## Persistence and Hosting

| Interface | Purpose |
| --- | --- |
| `ICheckpointStore` | Execution checkpoints |
| `IExecutionJournal` | Append-only event journal |
| `IExecutionReplayer` | Replay from checkpoints |
| `IExecutionScheduler` | Queue, prioritize, schedule executions |
| `IShutdownHook` / `IShutdownCoordinator` | Graceful shutdown |
| `IWorkflowExecutor` | DAG workflow execution |
| `IRouterAgent` | Multi-agent dispatch |

## Observability

| Interface | Purpose |
| --- | --- |
| `IMetricsCollector` | Structured metrics |
| `IDiagnosticCollector` | Diagnostic traces |
| `IStartupAnalyzer` | DI configuration validation at startup |
| `IExecutionEventPublisher` / `IExecutionEventHandler` | Execution event bus |
| `IRecoveryStrategy` | Failure recovery strategies |
