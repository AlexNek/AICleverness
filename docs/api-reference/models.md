# Models

All records and DTOs live in `AiCleverness.Models`.

## Request and Result

| Type | Properties |
| --- | --- |
| `AgentRequest` | `Goal`, `AllowedToolNames`, `Parameters`, `AgentName` |
| `AgentResult` | `Success`, `Output`, `Reasoning`, `Steps`, `Usage`, `Metadata` |
| `DecisionResult` | `Decision`, `Approved`, `Confidence`, `Reasoning` |
| `PolicyResult` | `Applied`, `Score`, `Recommendation`, `Reasoning` |
| `StrategyResult` | `Success`, `Output`, `Reasoning`, `Artifacts` |
| `PlannedStep` | `Name`, `Type`, `Description`, `Parameters` |
| `QualityGateResult` | `Approved`, `Retry`, `Reason`, `ReplacementResult` |
| `ValidationResult` | `IsValid`, `Error` |
| `InputValidationResult` | `IsValid`, `Error` |

## Tools

| Type | Properties |
| --- | --- |
| `ToolDefinition` | `Name`, `Description`, `Parameters` (JSON schema), metadata (`Category`, `Version`, `DefaultTimeout`, etc.) |
| `ToolInvocation` | `Name`, `Arguments`, `InvocationId` |
| `ToolResult` | `Success`, `Output`, `Error` |
| `ToolExecutionPolicy` | `MaxRetries`, `Timeout`, `LogEnabled`, `MetricsEnabled` |
| `CompletedToolCall` | `Id`, `Name`, `Arguments` — flushed from streaming buffer |
| `StreamingToolCallUpdate` | `ToolCallId`, `FunctionName`, `ArgumentsChunk` — partial streaming input |
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
| `AgentEvent` (and subtypes) | Streaming events: `ModelChunkEvent`, `ToolCompletedEvent`, etc. |
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

## Workflows

| Type | Properties |
| --- | --- |
| `WorkflowDefinition` | `Name`, `Nodes` |
| `WorkflowNode` | Name, type, parameters |
| `WorkflowResult` | Per-node outputs and overall status |
