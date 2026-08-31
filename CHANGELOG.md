# Changelog

All notable changes to this project will be documented in this file. Date format: YYYY-MM-DD

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Decision transcripts now include a bounded `State properties` subsection for non-null decision-tree action state values, with deterministic formatting for nested dictionaries and enumerable values, safe Markdown escaping, and normal-mode redaction/omission diagnostics.

### Changed
- **Breaking:** `IAgentMemory` is no longer available as a shared core DI singleton; applications requiring memory must integrate it explicitly per execution.
- **Breaking:** Decision-tree actions are now supplied as explicit `IDecisionAction` instances to each `DecisionTreeExecutor.ExecuteAsync` call instead of being registered as shared DI services; action registration is no longer required.
- Clarified that `ActionFailed` remains in the public decision-tree outcome enum for compatibility but is not emitted by the current executor after a handled action failure follows a valid fallback path.

### Fixed
- Decision-tree execution now reports `Terminal` and success when a handled action failure follows a planned fallback path to a terminal node, without carrying the recovered action error into the result; classification errors remain preserved for unknown outcomes.

## [1.7.0] - 2026-08-31

### Added
- `DecisionTreeResult.StateProperties` exposes a snapshot of non-null action-produced execution state values through a read-only dictionary while preserving the existing result constructor for binary compatibility.
- Provider overload and capacity diagnostics now support additive `LlmProviderException` metadata, application-owned provider error/status classification mappings, hard-permanent status handling, failover advancement, nullable diagnostics on LLM call observers, bus events, and streaming failure events for each failed attempt, and an unambiguous completion-pipeline constructor with optional classification mappings.

### Changed
- Production library types are now organized into one type per source file, preserving public APIs, record constructors, enum values, event discriminators, and JSON serialization contracts.
- Runtime lifecycle statuses, LLM message roles, built-in planner vocabulary, transcript persistence/redaction vocabulary, parser and formatting limits, idempotency hash format, decision-data representation overhead, decision classification retry bounds, workflow defaults, and metrics percentile thresholds now have narrow named owners without changing observable behavior.
- **Breaking:** Decision-tree contracts now use `ActionKey`/`PredicateKey` instead of `ActionName`/`PredicateName`, and action and predicate implementations expose `Key` instead of `Name`. Decision trees and nodes now support optional `Name`/`Description` metadata. Migrate C# references and implementations, and rename JSON `actionName`/`predicateName` fields to `actionKey`/`predicateKey`.
- Decision transcripts now normalize valid language-tagged and bare Markdown fences around structured LLM responses into one readable JSON block.

### Fixed
- Restored legacy completion-pipeline constructor overloads and made tool input path and size validation fail closed for invalid or unmeasurable arguments.

## [1.6.0] - 2026-08-28

### Added
- Bounded decision-tree classification context and transcript policies: metadata, content, aggregate representation, and display-field limits; separate truncation diagnostics; read-only builder snapshots; post-redaction transcript limits with deterministic omission markers; terminal-result preservation; eager configuration validation; and explicit bounded debug-transcript behavior.

### Changed
- **Breaking:** `IDecisionLlmContextBuilder` now receives a read-only `DecisionDataSnapshot` instead of the mutable execution `DataStore`; stable evidence identifiers remain available while bounded display values are provided for prompt rendering.
- **Breaking:** Renamed decision-tree question nodes to classify nodes across the public API, JSON contract, transcript builders, and journal/event-bus event strings; consumers must migrate `Question`/`question` names to `Classify`/`classify` and `Task`/`task`, and rename classification-completed event types.

### Fixed
- Decision-tree execution now supports configured model failover through `DecisionTreeExecutionOptions`, using an explicit primary model and ordered fallback-only chain for classifier-recognized transient failures; the active model and remaining candidates persist across classification nodes within one execution.
- Decision-tree classification answers now accept valid JSON wrapped in language-tagged or bare Markdown code fences while preserving bounded-answer validation.
- Decision-tree classification now fails closed when preparation omits any user context message, preventing incomplete provider requests while preserving normal-sized decision data.

## [1.5.0] - 2026-08-26

### Added
- Generic decision-tree execution for declarative JSON workflows with action, question, condition, and terminal nodes; bounded LLM classification with explicit unknown handling; application action and predicate extensions; resource budgets; journal and event-bus records; opt-in normal/debug Markdown transcripts with `/t` and `/d` demo switches; AOT-compatible loading; DI registration; and a hermetic demo workflow with usage documentation.

## [1.4.1] - 2026-08-23

### Fixed
- Debug Markdown transcripts now record the effective system prompt once per execution instead of repeating it in request parameters and quality-retry runtime sections

## [1.4.0] - 2026-08-19

### Added
- Opt-in per-execution Markdown transcripts with an explicitly supplied absolute destination, one file per execution, local-time host-redacted and filesystem-safe human-readable task-goal filenames with numeric collision suffixes, separate turn quality/failover metadata, terminal sections for returned and escaped failure/cancellation paths, tool-call IDs and raw malformed-argument preservation, debug runtime/request metadata, result metadata for the completed path/status, and host-configured redaction that fails closed when unavailable
- The hermetic demo now supports `/t` for normal Markdown transcripts and `/d` for debug transcripts, with long-form aliases, writing artifacts to a `transcripts` directory relative to the demo executable
- Tool-loop progress now includes a bounded first-line summary for successful non-empty tool results and deterministic model/tool/argument decision metadata before valid tool calls; malformed or non-object argument payloads no longer abort progress handling; full tool output sent to the LLM remains unchanged
- Optional pre-invocation idempotency replay now returns cached tool results before real-call reporting, suppressing normal invocation lifecycle events and counters while preserving the complete result for the next LLM turn

## [1.3.3] - 2026-08-19

### Changed
- `DefaultLlmErrorClassifier` now classifies HTTP 5xx server errors and HTTP 429 rate limits as `TransientAdvance`, enabling in-place model failover for provider outages instead of aborting immediately
- **Breaking:** `FailureClassification` enum renamed to `EFailureClassification` to follow project naming conventions — consumers must update type references including `LlmCallInfo.Classification`

## [1.3.2] - 2026-08-17

### Added
- Surface LLM reasoning text in progress output when the model generates explanations alongside tool calls

## [1.3.1] - 2026-08-17

### Added
- `EFailureKind` enum (`None`, `NoFailure`, `LlmTimeout`, `LlmError`, `FailoverExhausted`, `TurnLimitExceeded`, `Cancelled`, `PolicyBlocked`, `InputValidationFailed`, `Unknown`) and corresponding property on `AgentResult` — enables typed failure assertions without relying on error message string content. `None` (default) means "not set"; `NoFailure` explicitly indicates successful execution; `Unknown` is a catch-all for unclassified failures

### Changed
- Timeout error messages in both streaming and buffered LLM call strategies now report `"no response received (model may be unavailable or overloaded)"` instead of the generic `"timed out"`. Streaming additionally reports chunk count when stalling mid-stream. Failover verb changes from `timed out` to `unavailable` when the model never responded
- `AgentRuntime` constructor parameter `ILogger<AgentRuntime>?` replaced by `ILoggerFactory?` — the runtime and all internal components (`StreamingLlmCallStrategy`, `BufferedLlmCallStrategy`) create their own typed `ILogger<T>` from the factory. Callers pass one factory; classes never call `loggerFactory.CreateLogger<T>()` externally. The parameter is optional and backward-compatible; when omitted, all components run without logging

### Fixed
- Original provider exception is now preserved as `InnerException` on timeout `OperationCanceledException` and logged at `Warning` level in both streaming and buffered strategies — previously the provider error was silently discarded

## [1.3.0] - 2026-08-16

### Added
- Streaming LLM client support via `IStreamingLlmClient : ILlmClient` — opt-in interface returning `IAsyncEnumerable<LlmChunk>` with idle-based timeout semantics. Strategy pattern (`ILlmCallStrategy`, `BufferedLlmCallStrategy`, `StreamingLlmCallStrategy`) replaces inline timeout logic in `LlmToolLoop` — resolved at construction time via `LlmCallStrategyFactory`, no runtime type checks in the loop
- `LlmChunk` record (content delta, tool-call deltas, completion flag, optional usage) and `LlmToolCallDelta` record (index-based incremental tool-call fragments)
- `StreamingToolCallAccumulator` — assembles `LlmToolCallDelta` fragments by index into complete `LlmToolCall` instances
- `AgentPropertyKeys.IdleTimeoutSeconds` / `AgentRuntimeOptions.DefaultIdleTimeoutSeconds` (default 30s) — silence threshold between meaningful chunks during streaming. `CompletionTimeoutSeconds` remains the absolute wall-clock safety cap
- Intermediate `ModelChunkEvent` with `IsFinal = false` emitted during streaming for real-time UX updates

## [1.2.0] - 2026-08-16

### Added
- Capability-based model failover: on transient LLM failure (timeout), the runtime fails over to the next candidate model in the ordered chain — same conversation, same turn, no repeated tool calls. Opt-in via `AgentRuntimeOptions.EnableModelFailover` or per-request `enable_model_failover` parameter. Candidate chain built from `ModelResolutionResult.Fallbacks` (capability resolution) or explicit `model_fallback_chain` request property — explicit names are validated against the registered model catalog (`IModelCatalog.FindByNameAsync`), unknown names are skipped with a warning, and the chain is normalized — the active model is excluded and duplicates removed, so failover never retries the current model. A model set via `model` without capability resolution stays pinned and never fails over
- `LlmCallInfo` record and `IAgentObserver.OnLlmCallCompletedAsync` hook — fires exactly once per LLM call attempt (success, error, or timeout) with full context: model, turn, attempt, duration, usage, classification
- `IAgentObserver.OnModelSwitchedAsync` — observer notification on every model failover switch
- `ModelSwitchedAgentEvent` (streaming) and `ModelSwitchedBusEvent` (bus) — model-switch events for all observability channels
- `FailureClassification` enum and internal `ILlmErrorClassifier` / `DefaultLlmErrorClassifier` — extensible error classification (timeout → advance; extension point for rate-limit/unavailable signals)
- `ModelResolutionResult.Fallbacks` — ordered alternate models from capability resolution
- `ModelExecutionInfo.RemainingFallbacks` — provenance tracking for failover depth
- `LlmFailedEvent` execution event — failed LLM attempts (timeout/error) are recorded in manifests/journals with error, duration, and logical turn instead of being represented as responses; `LlmRespondedEvent` also carries the logical `Turn`. `DefaultMetricsCollector` counts successful and failed attempts alike in `TotalLlmCalls` / `AverageLlmDuration`

### Changed
- `LlmCallCompletedBusEvent` is now published for every LLM attempt — success, timeout, and error (previously successful completions only) — and carries `Success`, `Turn`, and `Error` alongside duration and usage, so bus-based attempt metrics see every attempt

## [1.1.0] - 2026-08-15

### Fixed
- Tool calls are now enforced at execution time, not only when the tool list is sent to the model: a tool excluded by `AllowedToolNames` can no longer run even if the model names it anyway (the call is rejected and reported back to the model)
- `OpenTelemetryObserverSample` no longer logs an unrestricted `AllowedToolNames` (`null`) as `tool_count=0`; the run-start log now carries a `tool_selection` state (`unrestricted` / `none` / `named`) alongside the count

### Changed
- `AgentRequest.AllowedToolNames` now distinguishes `null` (unrestricted — every registered tool is available) from an empty list (no tools at all); previously both meant unrestricted — callers building the list dynamically who want unrestricted access when nothing is added should pass `null` instead of an empty list

## [1.0.0] - 2026-08-14

### Added
- Provider-neutral AI execution runtime with `IAgentRuntime` orchestrator
- `ILlmClient` abstraction for any AI provider adapter
- `ITool` interface with `ToolDefinition`, `ToolInvocation`, and `ToolResult` models
- Tool execution with `IToolExecutor`, timeout, retries, and idempotency cache
- Streaming tool call buffer (`ToolCallBuffer`) for partial JSON accumulation
- `IAgentPolicy` pipeline for pre-execution guardrails
- `IAgentStrategy` for deterministic shortcuts bypassing the LLM
- `IAgentPlanner` with default and sequential planner implementations
- `IAgentQualityGate` for output quality evaluation with retry support
- `IAgentResultValidator` and `IAgentResultTransformer` post-processing
- `IAgentObserver` for lifecycle telemetry and OpenTelemetry integration
- Tiered memory: `IWorkingMemory`, `ILongTermMemory`, `IVectorMemory`, `IAggregateMemory`
- Security: `IPromptGuard`, `IToolCallValidator`, `IOutputGuard`, `IApprovalService`, `IScopeValidator`
- Agent-scoped extension points with predicate-based registration
- Streaming execution via `IStreamingAgentRuntime` and `IAsyncEnumerable<AgentEvent>`
- Workflow engine: `WorkflowDefinition`, `WorkflowNode`, sequential executor
- Persistence: `ICheckpointStore`, `IExecutionJournal`, `IExecutionReplayer`
- Hosting: `IExecutionScheduler`, `IShutdownCoordinator`, `HostedAgentRuntimeService`
- Observability: `IMetricsCollector`, `IDiagnosticCollector`, `IStartupAnalyzer`
- Dependency injection extensions: `AddAiClevernessRuntime()` and per-concern registrations
- NuGet packaging with GitVersion, SourceLink, and symbol packages
- GitHub Actions workflow for CI build, test, and NuGet publish on tag
- Unit tests with xUnit and FluentAssertions
- Benchmarks project with BenchmarkDotNet
- Developer manual published as a documentation site via MkDocs Material and GitHub Pages
- NuGet package ships a compact README with use cases and prominent links to the developer manual and the full repository README

[Unreleased]: https://github.com/AlexNek/AICleverness/compare/v1.7.0...HEAD
[1.7.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.7.0
[1.6.0]: https://github.com/AlexNek/AICleverness/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.5.0
[1.4.1]: https://github.com/AlexNek/AICleverness/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.4.0
[1.3.3]: https://github.com/AlexNek/AICleverness/releases/tag/v1.3.3
[1.3.2]: https://github.com/AlexNek/AICleverness/releases/tag/v1.3.2
[1.3.1]: https://github.com/AlexNek/AICleverness/releases/tag/v1.3.1
[1.3.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.3.0
[1.2.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.2.0
[1.1.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.1.0
[1.0.0]: https://github.com/AlexNek/AICleverness/releases/tag/v1.0.0
