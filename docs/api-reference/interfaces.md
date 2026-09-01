# Core Interfaces

Most public extension interfaces live in `AiCleverness.Abstractions`; transcript
formatting and persistence contracts live in `AiCleverness.Runtime.Transcript`.
This page lists them grouped by topic.

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
| `IAgentInputValidator` | Checks the input; registered for all agents by default, or for one agent via an `appliesTo` condition |
| `IAgentPlanner` / `INamedAgentPlanner` | Splits the goal into steps |
| `IPlannerRegistry` | Finds a named planner |
| `IAgentStrategy` / `IStrategyRegistry` | Answers the goal without the LLM |
| `IAgentQualityGate` | Checks the answer; can ask for a retry |
| `IAgentResultValidator` | A simple yes/no check on the final result |
| `IAgentResultTransformer` | Changes the final output (formatting, removing private data) |
| `IAgentObserver` | Gets messages about the run: start, finish, errors. New: `OnLlmCallCompletedAsync`, `OnModelSwitchedAsync` (default no-op) |
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

## Decision Trees

| Interface | What it does |
| --- | --- |
| `IDecisionTreeLoader` | Loads and validates a declarative decision-tree definition from the application's source or storage |
| `IDecisionAction` | Performs an application-defined action for an action node and returns `DecisionActionResult` |
| `IDecisionPredicate` | Evaluates an application-defined predicate used to choose a transition |
| `IDecisionDataPolicy` | Selects and bounds the data represented in a classification context |
| `IDecisionLlmContextBuilder` | Builds the bounded LLM context used by classification nodes |

Decision-tree execution is provided by the registered `DecisionTreeExecutor`
class and configured with `AddDecisionTreeExecution`. The execution options
configure tree budgets, model/failover behavior, data policy, transcript
policy, and optional transcript factories. Decision actions can return both
`Error` and `OutcomeSummary`; the latter is informational and is available to
transcript builders without changing the action status.

## Transcript Extension Contracts

These public interfaces are in `AiCleverness.Runtime.Transcript` and are
configured through `AgentRuntimeOptions` or `DecisionTreeExecutionOptions`.

| Interface | What it does |
| --- | --- |
| `ITranscriptBuilder` | Renders each transcript section; supports headers, decision overviews, debug data, turns, model/tool sections, decision actions/classifications/LLM attempts, results, retries, status, final results, and final failures |
| `ITranscriptSink` | Persists rendered sections through `FilePath`, `Append`, `Complete`, and `Dispose`; it can target a file or any other destination |
| `TranscriptBuilderDecorator` | Delegates all builder methods to an inner builder and exposes virtual methods so applications can customize selected sections without reimplementing `ITranscriptBuilder` |

A builder is selected with `TranscriptBuilderFactory` and a sink with
`TranscriptSinkFactory`. Both delegates are invoked for every enabled
execution. The sink factory receives the intended logical transcript path;
custom sinks may use it as an identity while writing to a database, queue,
object store, or memory. When no factory is configured, the default
`MarkdownTranscriptBuilder` and `FileTranscriptSink` are used.

For a small Markdown customization, derive from `TranscriptBuilderDecorator`.
Its default constructor wraps a new `MarkdownTranscriptBuilder`, and its
virtual methods delegate all unmodified sections automatically. Override only
the section that needs different formatting, then return a fresh decorator from
`TranscriptBuilderFactory`. Implement `ITranscriptBuilder` directly only when
the application needs a completely different representation.

Transcript components are execution-scoped. Factories must return fresh
instances and must not return cached mutable objects. Applications must not
register transcript builders, sinks, factory results, or transcript contexts
as singletons. Rendering and persistence failures are best-effort failures:
they disable transcript persistence for that execution but do not replace the
primary agent or decision-tree result. Normal-mode values are redacted before
custom builders/sinks receive them; explicit debug mode bypasses redaction and
should be used only with controlled data. See [Decision transcript
configuration](../execution/decision-trees.md#decision-transcripts) for
complete examples.

## Observability

| Interface | What it does |
| --- | --- |
| `IMetricsCollector` | Numbers about the runs: count, duration, success rate |
| `IDiagnosticCollector` | Detailed traces for debugging |
| `IStartupAnalyzer` | Checks the DI setup when the application starts |
| `IExecutionEventPublisher` / `IExecutionEventHandler` | Publishes run events to subscribers |
| `ILlmErrorClassifier` | Internal: classifies LLM failures as transient or permanent for failover decisions |
| `IRecoveryStrategy` | How to recover after a failure |
