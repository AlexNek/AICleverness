# Core Interfaces

All public interfaces live in `AiCleverness.Abstractions`. This page lists
them grouped by topic.

## Execution

| Interface | What it does |
| --- | --- |
| `IAgentRuntime` | Runs one agent execution: `RunAsync(request, progress, cancellationToken)` |
| `IStreamingAgentRuntime` | Same as `IAgentRuntime`, but returns events as they happen (`IAsyncEnumerable<AgentEvent>`) |
| `IAgentContext` | The context of one run: goal, agent name, parameters, memory |
| `IAgentDecision` | The result of a decision |

## LLM and Tools

| Interface | What it does |
| --- | --- |
| `ILlmClient` | The connection to your AI provider — the only interface you must implement |
| `ITool` | A tool: name, description, JSON schema, and `InvokeAsync` |
| `IToolRegistry` | Stores all tools and finds them by name |
| `IToolExecutor` | Runs tool calls with timeout, retries, and validation |
| `ICompensatingTool` | A tool that can undo an earlier call of itself |
| `IToolCallValidator` | Checks a tool call before it runs |

## Pipeline Extension Points

| Interface | What it does |
| --- | --- |
| `IAgentPolicy` | A guard before the run; can stop the run |
| `IAgentInputValidator` | Checks the input (can be registered for one agent only) |
| `IAgentPlanner` / `INamedAgentPlanner` | Splits the goal into steps |
| `IPlannerRegistry` | Finds a named planner |
| `IAgentStrategy` / `IStrategyRegistry` | Answers the goal without the LLM |
| `IAgentQualityGate` | Checks the answer; can ask for a retry |
| `IAgentResultValidator` | A simple yes/no check on the final result |
| `IAgentResultTransformer` | Changes the final output (formatting, removing private data) |
| `IAgentObserver` | Gets messages about the run: start, finish, errors |
| `IAgentPipelineMiddleware` | Your own step in the pipeline |

## Memory

| Interface | What it does |
| --- | --- |
| `IAgentMemory` | Simple key-value storage for agents |
| `IWorkingMemory` | Temporary storage that lives only during one run |
| `ILongTermMemory` | Storage that survives between runs |
| `IVectorMemory` | Search by meaning (embeddings), not by exact words |
| `IAggregateMemory` | One entry point to all three memory types |

## Security

| Interface | What it does |
| --- | --- |
| `IPromptGuard` | Checks incoming prompts: injection attacks, jailbreak attempts, private data |
| `IOutputGuard` | Checks the output: leaked secrets, dangerous content |
| `IApprovalService` | Pauses the run so a human can approve or reject it, then continues |
| `IScopeValidator` | Limits what a tool can touch (paths, hosts, size, writes) |
| `IIdempotencyCache` | Stops the same tool call from running twice |

## Models, Capabilities, Prompts

| Interface | What it does |
| --- | --- |
| `IModelCatalog` / `IModelManager` | The list of known models and their state |
| `IModelSelectionPolicy` / `IModelSelectionStrategy` | Rules for choosing a model |
| `ICapabilityResolver` | Finds out what a provider can do |
| `IPromptTemplate` / `IPromptRenderer` | Prompt templates and filling them with values |
| `IConversationManager` | Keeps the conversation history |
| `ISummarizationStrategy` / `ITruncationStrategy` | Keeps the conversation short enough for the model's context window |

## Persistence and Hosting

| Interface | What it does |
| --- | --- |
| `ICheckpointStore` | Saves the state of a run so it can continue later |
| `IExecutionJournal` | A log of all events; entries are only added, never changed |
| `IExecutionReplayer` | Runs a saved execution again |
| `IExecutionScheduler` | Orders and schedules runs |
| `IShutdownHook` / `IShutdownCoordinator` | Clean shutdown without losing running work |
| `IWorkflowExecutor` | Runs workflows of connected steps |
| `IRouterAgent` | Sends a request to the right agent |

## Observability

| Interface | What it does |
| --- | --- |
| `IMetricsCollector` | Numbers about the runs: count, duration, success rate |
| `IDiagnosticCollector` | Detailed traces for debugging |
| `IStartupAnalyzer` | Checks the DI setup when the application starts |
| `IExecutionEventPublisher` / `IExecutionEventHandler` | Publishes run events to subscribers |
| `IRecoveryStrategy` | How to recover after a failure |
