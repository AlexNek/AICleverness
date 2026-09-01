# Decision Trees

A decision tree is a declarative workflow for bounded classification and branching. The tree definition contains action, classify, condition, and terminal nodes. The library executes the workflow; your application supplies the LLM client, domain actions, and any domain-specific predicates.

Decision-tree APIs are in these namespaces:

- `AiCleverness.Models.DecisionTree` — tree, node, state, data, budget, event, and result models.
- `AiCleverness.Abstractions` — action, predicate, loader, context-builder, and completion-pipeline contracts.
- `AiCleverness.Runtime.DecisionTree` — the executor, JSON loader, parser, built-in predicates, and default context builder.
- `AiCleverness.DependencyInjection` — registration extensions.

## How execution works

The executor follows this sequence:

1. Load and validate the tree.
2. Create fresh state, data, resource usage, and conversation state for the run.
3. Visit the start node.
4. Execute actions, run bounded classifications, or evaluate predicates.
5. Follow the transition matching the node outcome.
6. Stop at a terminal node, an explicit `unknown`, cancellation, validation failure, or a resource budget limit.

A classification response must be JSON with an allowed answer:

```json
{
  "answer": "supported",
  "observation": "The evidence matches the requested capability.",
  "confidence": "high"
}
```

The parser accepts answer values case-insensitively and records the declared answer value. Malformed JSON or an answer outside the allowed list is retried once. A second invalid response follows the classify node's `unknown` transition.

## Register the runtime with DI

DI is the recommended setup. Register the existing core runtime and your provider adapter, then add decision-tree services:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.DependencyInjection;
using AiCleverness.Models.DecisionTree;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
services.AddDecisionTreeExecution(options =>
{
    options.DefaultMaxNodeVisits = 20;
    options.DefaultMaxLlmCalls = 10;
    options.DefaultMaxElapsedTime = TimeSpan.FromSeconds(120);
    options.DefaultMaxContextTokens = 4000;
    options.EnableModelFailover = true;
    options.Model = "primary-model";
    options.ModelFallbackChain = ["fallback-model"];
});

// Application actions are explicit per-execution instances.
using var provider = services.BuildServiceProvider();
```

When decision-tree model failover is enabled, `Model` is the explicit primary model for the first request and `ModelFallbackChain` is an ordered fallback-only list; do not include the primary in that list. The selected model and remaining fallback candidates are preserved across all classification nodes in one tree execution. Only failures recognized by the shared LLM error classifier (such as completion timeouts, HTTP 5xx, HTTP 429, and recognized rate-limit signals) advance to the next candidate. Disabled or incomplete failover configuration preserves the no-context completion behavior.

Custom `ILlmCompletionPipeline` implementations remain source-compatible through the default context overload, but must override that overload to consume execution services and apply model failover. A custom implementation that uses only the legacy overload intentionally receives no execution context or shared failover policy.

`AddDecisionTreeExecution()` registers the default `ILlmCompletionPipeline`, a transient default conversation manager, the loader, parser, default classify context builder, in-memory journal, in-memory event publisher, and built-in predicates. `AddAiClevernessLlmClient<T>()` remains the provider-neutral LLM adapter used by the default decision completion pipeline.

Application actions are supplied as explicit `IDecisionAction` instances to each `DecisionTreeExecutor.ExecuteAsync` call. They are not registered in the DI container, so each execution can use its own action instances and mutable action state cannot leak between executions. Predicates remain registered as catalog entries; keep predicate state out of fields as well.

## Define a tree as JSON

The loader uses source-generated `System.Text.Json` metadata. A valid tree has a non-empty `treeId`, a positive `version`, a `startNodeId`, and a non-empty `nodes` dictionary. The dictionary key is the canonical node ID.

This example collects evidence, runs a bounded classification, checks the evidence with a built-in predicate, and returns a verdict:

```json
{
  "treeId": "evidence-classification",
  "version": 1,
  "startNodeId": "collect",
  "systemPrompt": "Classify the evidence using exactly one allowed answer and return JSON.",
  "budget": {
    "maxNodeVisits": 10,
    "maxLlmCalls": 1,
    "maxElapsedTime": "00:00:30",
    "maxContextTokens": 1000,
    "onExceeded": "halt"
  },
  "nodes": {
    "collect": {
      "type": "action",
      "actionKey": "collectEvidence",
      "transitions": [
        { "condition": "success", "nextNodeId": "classify" },
        { "condition": "transientFailure", "nextNodeId": "failed" },
        { "condition": "permanentFailure", "nextNodeId": "failed" }
      ]
    },
    "classify": {
      "type": "classify",
      "task": "Is this evidence relevant to the requested capability?",
      "answers": ["supported", "unsupported"],
      "transitions": [
        { "condition": "supported", "nextNodeId": "verify" },
        { "condition": "unsupported", "nextNodeId": "rejected" },
        { "condition": "unknown", "nextNodeId": "unknown" }
      ]
    },
    "verify": {
      "type": "condition",
      "predicateKey": "dataExists",
      "predicateParameters": {
        "type": "evidence"
      },
      "transitions": [
        { "condition": "true", "nextNodeId": "approved" },
        { "condition": "false", "nextNodeId": "failed" }
      ]
    },
    "approved": {
      "type": "terminal",
      "verdict": "supported"
    },
    "rejected": {
      "type": "terminal",
      "verdict": "unsupported"
    },
    "unknown": {
      "type": "terminal",
      "verdict": "unknown"
    },
    "failed": {
      "type": "terminal",
      "verdict": "failed"
    }
  }
}
```

Required transitions are exact and case-sensitive in the tree definition:

| Node type | Required transition conditions |
| --- | --- |
| `action` | `success`, `transientFailure`, `permanentFailure` |
| `classify` | Every value in `answers`, plus `unknown` |
| `condition` | `true`, `false` |
| `terminal` | No transitions |

The loader rejects missing targets, duplicate conditions or answers, invalid node fields, unreachable nodes, and reachable cycles that cannot reach a terminal node. Cycles that have a path to a terminal node are allowed because resource limits bound execution.

## Load and execute a tree

Resolve the loader and executor from the provider. Loading validates the JSON tree structure and predicate catalog; action keys are validated when the explicit action instances are supplied for execution:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

var loader = provider.GetRequiredService<IDecisionTreeLoader>();
var executor = provider.GetRequiredService<DecisionTreeExecutor>();
var cancellationToken = CancellationToken.None;

var json = await File.ReadAllTextAsync("evidence-classification.json");
var tree = loader.Load(json);
var actions = new IDecisionAction[] { new CollectEvidenceAction() };

var result = await executor.ExecuteAsync(
    actions,
    tree,
    templateParameters: new Dictionary<string, string>
    {
        ["subject"] = "the requested capability"
    },
    cancellationToken: cancellationToken);

if (result.Succeeded)
{
    Console.WriteLine($"Verdict: {result.Verdict}");
}
else
{
    Console.WriteLine($"Decision ended as {result.Outcome}: {result.Error}");
}

Console.WriteLine($"Execution: {result.ExecutionId}");
Console.WriteLine($"Node visits: {result.Usage.NodeVisits}");
Console.WriteLine($"LLM calls: {result.Usage.LlmCalls}");
Console.WriteLine($"Tokens: {result.Usage.TotalTokens}");
```

`DecisionTreeResult` contains the execution ID, success flag, terminal verdict, outcome, parsed classifications, final `ResourceUsage`, an optional error, and `StateProperties`, which contains the non-null values produced by decision actions. The current executor emits `Terminal`, `Unknown`, `BudgetExhausted`, `Cancelled`, and `ValidationFailed`. `ActionFailed` remains in the public enum for compatibility with existing consumers, but is not emitted by the current executor when a handled action failure follows a valid fallback path.

Read action-produced values from the result with a null-safe lookup and type check:

```csharp
if (result.StateProperties?.TryGetValue("evidenceCollected", out var value) == true
    && value is string evidenceCollected
    && string.Equals(evidenceCollected, "true", StringComparison.Ordinal))
{
    Console.WriteLine("Evidence was collected.");
}
```

## Implement an action

Create action instances in the application composition code and pass the collection to `ExecuteAsync`; actions are not registered in the DI container. An action receives the node ID, execution ID, template parameters, mutable execution state, and execution-scoped `DataStore`. It returns a status and may add generic data or string properties:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

public sealed class CollectEvidenceAction : IDecisionAction
{
    public string Key => "collectEvidence";

    public Task<DecisionActionResult> ExecuteAsync(
        DecisionActionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var evidence = new DecisionData
        {
            Id = "evidence-1",
            Source = "application",
            Type = "evidence",
            Content = "Deterministic application evidence",
            CreatedAt = DateTimeOffset.UtcNow,
            ActionId = context.NodeId
        };

        return Task.FromResult(new DecisionActionResult(
            ProducedData: [evidence],
            Properties: new Dictionary<string, string>
            {
                ["evidenceCollected"] = "true"
            },
            Status: DecisionActionStatus.Success));
    }
}
```

Return `TransientFailure` or `PermanentFailure` when the action cannot complete. The tree must define both corresponding transitions. An action exception is converted to a permanent action failure by the executor.

## Use built-in or custom predicates

Built-in predicates are available without registration beyond `AddDecisionTreeExecution()`:

| Predicate | Parameters | Behavior |
| --- | --- | --- |
| `propertyExists` | `key` | True when a non-null state property exists |
| `propertyEquals` | `key`, `value` | True when a state property equals the declared value |
| `dataExists` | `type` | True when data of the declared type exists |
| `dataCountAtLeast` | `type`, `min` | True when at least `min` records of the type exist |

For application-specific logic, implement `IDecisionPredicate` and register it:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

public sealed class HasApprovalFlagPredicate : IDecisionPredicate
{
    public string Key => "hasApprovalFlag";

    public bool Evaluate(DecisionPredicateContext context)
        => context.State.Properties.TryGetValue("approved", out var value)
           && value is string text
           && string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
}

services.AddDecisionPredicate<HasApprovalFlagPredicate>();
```

A predicate receives the current node ID, state, data, and JSON parameters declared on the condition node. Predicate names must be unique across the built-in and application catalogs.

## Budgets and resource accounting

A tree's `DecisionBudget` maps to the existing `ResourceLimits` and `ResourceUsage` model:

| Decision budget | Usage or limit |
| --- | --- |
| `MaxNodeVisits` | `ResourceUsage.NodeVisits` / `ResourceLimits.MaxNodeVisits` |
| `MaxLlmCalls` | `ResourceUsage.LlmCalls` / `ResourceLimits.MaxLlmCalls` |
| `MaxElapsedTime` | `ResourceUsage.Duration` / `ResourceLimits.MaxDuration` |
| `MaxContextTokens` | Maximum context passed to conversation preparation; not a cumulative token limit |
| `OnExceeded` | `Halt`, `Warn`, or `Throttle` behavior |

`MaxContextTokens` is a preparation budget, not a guarantee that every classification prompt will be truncated successfully. Before prompt construction, the executor applies the configured `IDecisionDataPolicy` to create a bounded, read-only `DecisionDataSnapshot` for both the default and custom context builders. The default builder renders bounded display values for identifiers, types, and sources; the original stable values remain available on the snapshot items. The policy also bounds metadata and reports omitted, per-item-truncated, and aggregate-truncated counts. A custom policy is responsible for returning its own bounded representation. A custom `IDecisionLlmContextBuilder` must produce at least one user message; if it produces none, the executor returns an `Unknown` classification without calling the provider. If conversation preparation removes any user message produced for the current classification, the executor also does not call the provider; it records an `Unknown` classification with an actionable context-budget error and follows the classify node's `unknown` transition. Custom conversation managers must preserve the identity of required user messages.

`Halt` returns `DecisionTreeOutcome.BudgetExhausted`. `Warn` continues, and `Throttle` inserts a short delay before continuing. The executor checks cancellation and limits before externally observable work and after recording node visits or LLM usage.

Configure defaults for trees that retain the library's default budget values:

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.DefaultMaxNodeVisits = 30;
    options.DefaultMaxLlmCalls = 5;
    options.DefaultMaxElapsedTime = TimeSpan.FromSeconds(60);
    options.DefaultMaxContextTokens = 2000;
    options.DecisionDataPolicy.MaxItems = 50;
    options.DecisionDataPolicy.MaxContentLengthPerItem = 4000;
    options.DecisionDataPolicy.MaxAggregateRepresentationLength = 12000;
    options.DecisionDataPolicy.MaxFieldLength = 256;
    options.DecisionDataPolicy.MaxMetadataEntries = 20;
    options.DecisionDataPolicy.MaxMetadataKeyLength = 256;
    options.DecisionDataPolicy.MaxMetadataValueLength = 1000;
    options.DecisionTranscriptPolicy.MaxProducedItemsPerAction = 100;
    options.DecisionTranscriptPolicy.MaxContentLength = 4000;
    options.DecisionTranscriptPolicy.MaxMetadataEntries = 20;
    options.DecisionTranscriptPolicy.MaxMetadataKeyLength = 256;
    options.DecisionTranscriptPolicy.MaxMetadataValueLength = 1000;
    options.DecisionTranscriptPolicy.MaxMessageContentLength = 8000;
    options.DecisionTranscriptPolicy.MaxResponseContentLength = 8000;
    options.DecisionTranscriptPolicy.MaxTotalCharacters = 100000;
    options.TraceId = "decision-service";
    options.CorrelationId = "request-123";
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = false;
    options.TranscriptRedactor = text => text;
});
```

Prefer setting explicit budgets in each JSON tree when different workflows need different limits.

### Custom decision-data policy and context builder

The default policy is registered automatically. Custom policies and context builders must be registered before `AddDecisionTreeExecution()` because the default decision services use `TryAdd` registration; the first registration wins:

```csharp
services.AddSingleton<IDecisionDataPolicy, ApplicationDecisionDataPolicy>();
services.AddSingleton<IDecisionLlmContextBuilder, ApplicationDecisionContextBuilder>();
services.AddDecisionTreeExecution();
```

A custom `IDecisionDataPolicy` owns the bounds of the representation it returns. The executor does not reapply the default policy to an explicit custom policy. The context builder receives a read-only `DecisionDataSnapshot`, not the execution `DataStore`, and cannot add records to it. Stable `Id`, `Type`, and `Source` values are retained for correlation; use `DisplayId`, `DisplayType`, and `DisplaySource` when rendering bounded prompt text:

```csharp
public sealed class ApplicationDecisionContextBuilder : IDecisionLlmContextBuilder
{
    public IReadOnlyList<LlmMessage> Build(
        DecisionTree tree,
        DecisionNode classifyNode,
        DecisionState state,
        DecisionDataSnapshot data,
        IReadOnlyDictionary<string, string> templateParameters)
    {
        var evidence = string.Join(", ", data.GetAll().Select(item =>
            $"{item.DisplayId ?? item.Id} [{item.DisplayType ?? item.Type}] "
            + $"from {item.DisplaySource ?? item.Source}: {item.Content}"));
        return
        [
            new("system", tree.SystemPrompt ?? "Classify the request."),
            new("user", $"Task: {classifyNode.Task}\nData: {evidence}")
        ];
    }
}
```

The conversation is cumulative within one decision execution. Repeated classification nodes append new classification prompts, assistant responses, and retry instructions; selecting bounded data for a new prompt does not deduplicate or remove earlier conversation history. A custom builder must produce at least one user-role message, and a custom conversation manager must preserve the identity of retained required user messages.

## Journal, event bus, and graphs

Each node visit, action completion, and classification completion produces a journal event and a separate bus event. The journal operation happens before bus publication. Observability failures are best effort and do not change the tree result.

The journal records are:

- `DecisionNodeVisitedEvent`
- `DecisionActionCompletedEvent`
- `DecisionClassificationCompletedEvent`

The corresponding bus records have `BusEvent` suffixes. All records preserve the execution ID, timestamp, trace ID, and correlation ID.

`AddDecisionTreeExecution()` supplies in-memory implementations. Register custom implementations before it when persistence or a custom publisher is required; the default registrations use `TryAdd` semantics:

```csharp
services.AddExecutionJournal<DatabaseExecutionJournal>();
services.AddDecisionTreeExecution();
```

Decision journal events are recognized by `ExecutionGraph.FromEvents` and render as `DecisionNode` graph nodes rather than generic LLM nodes.

## AOT and trimming

Use `IDecisionTreeLoader.Load` rather than reflection-based JSON serialization. The library registers decision-tree models, transitions, budgets, and decision event records in its source-generated `AiClevernessJsonContext`. The documented camelCase JSON shape and string enum values are the supported format.

## Demo

The hermetic demo includes the evidence-classification workflow and does not call a network service:

```powershell
dotnet run --project AiClevernessLib.Demo
```

Look for the final section:

```text
Decision outcome: Terminal; verdict: supported; error: ; node visits: 4; LLM calls: 1
```

The demo action is intentionally application-local. Production applications should provide their own `IDecisionAction` and `IDecisionPredicate` implementations rather than referencing the demo project.

## Decision transcripts

Decision-tree transcripts are opt-in. When enabled, a run produces a Markdown transcript containing the decision-tree overview, node visits, action completions, classifications, LLM attempts, the terminal outcome, and resource usage. The same transcript extension points are also available to ordinary agent executions through `AgentRuntimeOptions`; the decision-tree configuration is separate because decision trees have their own policy limits and execution options.

### Configure the default transcript

Set an absolute directory through `DecisionTreeExecutionOptions`. A relative path is intentionally not accepted: transcript destinations should be explicit so a service does not unexpectedly write under whichever working directory hosts it.

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = false;
    options.TranscriptRedactor = text =>
        text.Replace("customer@example.invalid", "[REDACTED]", StringComparison.OrdinalIgnoreCase);

    options.DecisionTranscriptPolicy.MaxProducedItemsPerAction = 100;
    options.DecisionTranscriptPolicy.MaxContentLength = 4000;
    options.DecisionTranscriptPolicy.MaxMetadataEntries = 20;
    options.DecisionTranscriptPolicy.MaxMetadataKeyLength = 256;
    options.DecisionTranscriptPolicy.MaxMetadataValueLength = 1000;
    options.DecisionTranscriptPolicy.MaxMessageContentLength = 8000;
    options.DecisionTranscriptPolicy.MaxResponseContentLength = 8000;
    options.DecisionTranscriptPolicy.MaxTotalCharacters = 100000;
});
```

The default builder is `MarkdownTranscriptBuilder` and the default sink is `FileTranscriptSink`. The file name contains a local timestamp and a sanitized task/tree identity; the execution ID remains in the transcript content. `DecisionTreeResult` does not contain a transcript path because persistence is optional and sinks do not have to be files. The configured directory and the public journal/event records are the stable application-level ways to locate or observe the run.

For ordinary agent runs, use the equivalent runtime options:

```csharp
services.AddAiClevernessRuntime(options =>
{
    options.TranscriptRedactor = text =>
        text.Replace("customer@example.invalid", "[REDACTED]", StringComparison.OrdinalIgnoreCase);
});
```

An agent request must opt in with the absolute transcript-directory request parameter. Decision-tree runs opt in with `DecisionTreeExecutionOptions.TranscriptDirectory`. If no directory is configured, the primary execution still runs normally and no transcript is persisted.

### Normal and debug transcripts

Normal mode is the safe default for persisted content. It requires `TranscriptRedactor`; goals, model content, tool arguments and results, answers, observations, action errors, verdicts, and outcome information are redacted before they are sent to the transcript builder. A redactor must return text safe for persistence and must be safe to call from concurrent executions.

Debug mode is an explicit privacy decision. It bypasses the redactor and can include unredacted prompts, parameters, model responses, tool data, and decision data. Debug mode still applies every configured decision transcript size limit. Do not enable it for production data unless the destination and access controls are appropriate:

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = true;
});
```

Redaction is performed by the transcript runtime before normal-mode values reach a custom builder or sink. A custom builder is therefore not a substitute for configuring a redactor. A custom sink controls persistence, but it does not make debug output safe.

### Decision transcript limits

`DecisionTranscriptPolicy` protects the decision-specific part of a transcript. Its limits cover produced-data items and content, metadata entries and key/value lengths, prepared messages, model responses, and the optional `MaxTotalCharacters` budget. Limits are applied after normal-mode redaction, so redaction can only reduce the persisted content. Invalid policy values are rejected before execution.

When the total character budget omits a decision section, the terminal result records the omitted-section count. The terminal result has reserved space: it is not reported as written unless the builder actually produced output. Transcript persistence is best effort; reaching a transcript limit does not change the decision result.

### Custom builders

`ITranscriptBuilder` controls the representation of individual sections. It has methods for headers, decision overviews, debug information, turns, model content, tool decisions/results, decision actions/classifications/LLM attempts, terminal results, retries, status, final results, and final failures. Each method returns the content for one section. The built-in Markdown builder remains the default, but applications can return JSON fragments, HTML, plain text, or organization-specific Markdown.

For a small formatting change, derive from `TranscriptBuilderDecorator` instead of implementing the complete interface. Its parameterless constructor wraps a new `MarkdownTranscriptBuilder`, and every section delegates to that builder unless you override it:

```csharp
public sealed class ApplicationTranscriptBuilder : TranscriptBuilderDecorator
{
    public override string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData)
    {
        var markdown = base.DecisionAction(
            nodeId,
            actionKey,
            nodeName,
            status,
            outcomeSummary,
            error,
            producedData);

        return markdown.Replace(
            "### Decision action:",
            "### Application action:",
            StringComparison.Ordinal);
    }
}

services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptRedactor = text =>
        text.Replace("customer@example.invalid", "[REDACTED]", StringComparison.OrdinalIgnoreCase);
    options.TranscriptBuilderFactory = static () => new ApplicationTranscriptBuilder();
});
```

This preserves the built-in Markdown behavior for headers, model/tool sections, classifications, terminal results, failures, and every other section. Override only the methods you need. The decorator can also wrap another `ITranscriptBuilder` through its constructor when the application wants to layer multiple customizations. If the application needs a completely different representation, it can still implement `ITranscriptBuilder` directly.

Returning an empty string is a valid way for a custom format to omit a section; returning `null` is not supported. Builders should not retain state in static fields or be reused for another run.

Action sections use `DecisionNode.Name` when it is present and non-empty, and fall back to `ActionKey` when it is absent. An action can separately provide a human-readable `DecisionActionResult.OutcomeSummary`; that summary describes what the action found, changed, or decided and is not treated as an error. `Error` remains reserved for an actual action failure. For example:

```csharp
return new DecisionActionResult(
    ProducedData: data,
    Properties: new Dictionary<string, string> { ["classification"] = "supported" },
    Status: DecisionActionStatus.Success)
{
    OutcomeSummary = "The evidence supports the requested classification."
};
```

### Custom sinks and logical identity

`ITranscriptSink` controls where the builder output goes. It exposes the intended logical `FilePath`, `Append`, `Complete`, and `Dispose`. A sink can write to a database, queue, object store, test buffer, or another service instead of creating a local file:

```csharp
public sealed class MemoryTranscriptSink : ITranscriptSink
{
    private readonly StringBuilder _content = new();

    public MemoryTranscriptSink(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public void Append(string content) => _content.Append(content);

    public void Complete() { }

    public void Dispose() { }

    public string GetContent() => _content.ToString();
}

services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptRedactor = text => text;
    options.TranscriptSinkFactory = static logicalPath =>
        new MemoryTranscriptSink(logicalPath);
});
```

`logicalPath` is an identity, not a requirement that the sink create that file. It lets a custom destination preserve the same timestamp/goal naming convention and gives `ITranscriptSink.FilePath` a stable value for diagnostics. A database-backed sink can use the path as a correlation label while storing the actual sections elsewhere.

### Per-execution lifetime and failure behavior

Both factories are invoked for each enabled execution. The returned builder and sink belong only to that execution, and transcript rendering is serialized within that execution before content is appended. Do not register a builder, sink, factory result, or mutable transcript context as a singleton, and do not return a cached instance from a factory. This is required even when the application runs only one execution today: concurrent executions must never append to or expose one another's state.

A factory returning `null`, a builder exception, a sink exception, or a finalization exception disables transcript persistence for that execution and is logged as a persistence problem. It does not change the primary agent or decision-tree result. The runtime also disposes a sink if initialization fails. Treat transcript output as best-effort observability, not as the transaction boundary for the business operation.

### Demo output

The hermetic demo enables the same settings with the existing switches and does not call a network service:

```powershell
dotnet run --project AiClevernessLib.Demo -- /t  # normal, redacted transcript
dotnet run --project AiClevernessLib.Demo -- /d  # debug, unredacted transcript
```

`--transcript` aliases `/t`; `--transcript-debug` and `--debug-transcript` alias `/d`. If both are supplied, debug mode wins. The demo prints the resolved transcript directory after scenario 8. In a Debug build, the default files are under `AiClevernessLib.Demo/bin/Debug/net10.0/transcripts` (or the matching configuration/target-framework directory).

## Current integration boundaries

Decision-tree execution and the ordinary agent tool loop use the same registered `ILlmCompletionPipeline` boundary and shared model-failover handler. Their orchestration flows remain separate, but completion policy, transient-failure classification, fallback advancement, and model-switch notifications are centralized in the shared runtime.

Decision events are available through the public journal and event-bus contracts. Decision transcript output is provided through the opt-in `DecisionTreeExecutionOptions` settings described above; the executor still does not expose the internal transcript sink or context.
