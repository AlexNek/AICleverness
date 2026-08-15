# Changelog

All notable changes to this project will be documented in this file. Date format: YYYY-MM-DD

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]



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
