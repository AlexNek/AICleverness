# Models

All data types (records) live in `AiCleverness.Models`. This page lists
every public type and its properties.

## Request and Result

| Type | Properties |
| --- | --- |
| `AgentRequest` | `Goal`, `AllowedToolNames`, `Parameters`, `AgentName`, `CapabilityRequirements` |
| `CapabilityRequirements` | `Capabilities`, `Properties` — what the run needs from the model/provider; used to pick a suitable model |
| `Capabilities` | `CapabilityFlags`, `CostTier`, `MaxLatencyMs`, `MinContextWindow`, `QualityTier` (all optional — `null` means no constraint) |
| `AgentResult` | `Success`, `Output`, `Reasoning`, `Steps`, `Usage`, `Metadata` |
| `DecisionResult` | `Decision`, `Approved`, `Confidence`, `Reasoning` |
| `PolicyResult` | `Applied`, `Score`, `Recommendation`, `Reasoning` |
| `StrategyResult` | `Success`, `Output`, `Reasoning`, `Artifacts` |
| `PlannedStep` | `Name`, `Type`, `Description`, `Parameters` |
| `QualityGateResult` | `Approved`, `Retry`, `Reason`, `ReplacementResult` |
| `ValidationResult` | `IsValid`, `Error` |
| `InputValidationResult` | `IsValid`, `Error` |

### Tool selection contract (`AgentRequest.AllowedToolNames`)

| Value | Meaning |
| --- | --- |
| `null` (default) | All tools — every registered tool is offered to the model |
| Empty list | No tools at all — the model answers with text only |
| Named list | Only these tools are offered; names that match no registered tool are ignored |

## Tools

| Type | Properties |
| --- | --- |
| `ToolDefinition` | `Name`, `Description`, `Parameters` (JSON schema), metadata (`Category`, `Version`, `DefaultTimeout`, etc.) |
| `ToolInvocation` | `Name`, `Arguments`, `InvocationId` |
| `ToolResult` | `Success`, `Output`, `Error` |
| `ToolExecutionPolicy` | `MaxRetries`, `Timeout`, `LogEnabled`, `MetricsEnabled` |
| `CompletedToolCall` | `Id`, `Name`, `Arguments` — a complete tool call, built from the streaming parts |
| `StreamingToolCallUpdate` | `ToolCallId`, `FunctionName`, `ArgumentsChunk` — one part of a tool call received while streaming |
| `DangerLevel` | `Safe`, `Low`, `Medium`, `High`, `Critical` |
| `ToolInputScope` | `AllowedPaths`, `AllowedHosts`, `MaxInputSizeBytes`, `AllowWrites`, etc. |

## LLM

| Type | Properties |
| --- | --- |
| `LlmMessage` | `Role`, `Content`, `ToolCalls`, `ToolCallId` |
| `LlmResponse` | `Content`, `ToolCalls`, `Usage` |
| `LlmTokenUsage` | `PromptTokens`, `CompletionTokens` |

## Execution State

| Type | Properties |
| --- | --- |
| `ExecutionStatus` | `Created`, `Validating`, `Planning`, `Executing`, `Completed`, `Failed`, `Cancelled`, ... |
| `AgentExecutionState` | `ExecutionId`, `Status`, `Metadata`, `State`, `Items`, `Artifacts` |
| `ExecutionEvent` | `ExecutionId`, `EventType`, `Timestamp`, `Data` |
| `AgentEvent` (and subtypes) | Streaming events: `ModelChunkEvent`, `ToolCompletedAgentEvent`, etc. |
| `ExecutionManifest` | `ExecutionId`, `Status`, `Duration`, `Events`, `Artifacts` |
| `ExecutionSnapshot` | `SchemaVersion`, `ExecutionId`, `Status`, `Goal`, counters, result |
| `ExecutionGraph` | `Nodes`, `Edges`, `ToMermaid()` export |

## Metrics, Diagnostics, Resources

| Type | Properties |
| --- | --- |
| `ExecutionMetrics` | `TotalExecutions`, `SuccessRate`, `P50/P95/P99Duration`, LLM/tool metrics |
| `DiagnosticReport` | `Entries`, `Categories`, severity levels |
| `CapabilityProfile` | `ProviderName`, `Capabilities`, `Limits` |
| `ResourceEstimate` / `ResourceUsage` / `ResourceLimits` | Cost, token, time, and tool-call budgets |

## Model Selection and Failover

| Type | Properties |
| --- | --- |
| `ModelResolutionResult` | `Model`, `Profile`, `Attempts`, `IsFallback`, `Fallbacks`, `SelectionReason` |
| `ModelExecutionInfo` | `Model`, `Profile`, `Attempt`, `IsFallback`, `RemainingFallbacks`, `SelectionReason` |
| `LlmCallInfo` | `ExecutionId`, `Model`, `Turn`, `Attempt`, `IsFallback`, `Duration`, `Usage`, `Success`, `Error`, `Classification`, `StartedAt` |
| `FailureClassification` | Enum: `Permanent`, `TransientAdvance` |
| `ModelSwitchedAgentEvent` | `ExecutionId`, `Turn`, `From`, `To`, `Reason` |
| `ModelSwitchedBusEvent` | `ExecutionId`, `From`, `To`, `Reason`, `Turn`, `Timestamp` |

## Workflows

| Type | Properties |
| --- | --- |
| `WorkflowDefinition` | `Name`, `Nodes` |
| `WorkflowNode` | Name, type, parameters |
| `WorkflowResult` | Per-node outputs and overall status |

## Decision Trees

Decision-tree models live in `AiCleverness.Models.DecisionTree`. They describe a
bounded, declarative workflow independently of the executor and LLM provider.

| Type | Properties and purpose |
| --- | --- |
| `DecisionTree` | `TreeId`, `Version`, `Name`, `Description`, `StartNodeId`, `Nodes`, `Budget`, `SystemPrompt`, `Task` |
| `DecisionNode` | `Type`, `Name`, `Description`, `ActionKey`, `Task`, `Answers`, `PredicateKey`, `PredicateParameters`, `Verdict`, `Transitions` |
| `DecisionTransition` | `Condition`, `NextNodeId`; the labeled edge followed after a node result |
| `DecisionBudget` | Limits for node visits, LLM calls, elapsed time, and context/resource use |
| `DecisionData` | A bounded piece of source data supplied to an action or classification |
| `DecisionClassification` | `NodeId`, `Answer`, `Observation`, `Confidence`, `At` |
| `DecisionActionResult` | `ProducedData`, `Properties`, `Status`, `Error`, and optional `OutcomeSummary` |
| `DecisionTreeResult` | `ExecutionId`, `Succeeded`, `Verdict`, `Outcome`, `Classifications`, `Usage`, `Error`, and execution-scoped `StateProperties` |
| `DecisionTreeOutcome` | Terminal, budget, cancellation, or failure category for the execution |
| `DecisionActionStatus` | Status reported by an application action, including success and failure states |
| `EDecisionNodeType` | Identifies whether a node performs an action, classification, predicate, or terminal operation |

`DecisionActionResult.OutcomeSummary` is deliberately separate from `Error`.
Use it to explain a successful or otherwise completed action in a transcript
without making the action look failed. `DecisionNode.Name` is the readable
label used for action headings when available; transcript rendering falls back
to `ActionKey` when the name is missing. `DecisionTreeResult.StateProperties`
contains execution-scoped properties collected from action results and is
separate from the list of classifications.

`DecisionDataPolicyOptions` limits which source data can enter a
classification prompt: item count, per-item and aggregate content, field
length, metadata, and optional type/source allow-lists. It protects prompt
construction and is not the same as transcript persistence policy.

`DecisionTranscriptPolicyOptions` limits decision-specific transcript output
after normal-mode redaction. It bounds produced data, metadata, prepared
messages, model responses, state properties, and optionally the total decision
section. These options affect what is persisted, not the primary
`DecisionTreeResult` or action execution. See [Decision Trees](../execution/decision-trees.md#decision-transcripts)
for configuration and examples.
