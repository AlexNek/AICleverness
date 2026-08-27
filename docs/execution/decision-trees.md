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

// Add application extensions before building the provider.
services.AddDecisionAction<CollectEvidenceAction>();

using var provider = services.BuildServiceProvider();
```

When decision-tree model failover is enabled, `Model` is the explicit primary model for the first request and `ModelFallbackChain` is an ordered fallback-only list; do not include the primary in that list. The selected model and remaining fallback candidates are preserved across all classification nodes in one tree execution. Only failures recognized by the shared LLM error classifier (such as completion timeouts, HTTP 5xx, HTTP 429, and recognized rate-limit signals) advance to the next candidate. Disabled or incomplete failover configuration preserves the no-context completion behavior.

Custom `ILlmCompletionPipeline` implementations remain source-compatible through the default context overload, but must override that overload to consume execution services and apply model failover. A custom implementation that uses only the legacy overload intentionally receives no execution context or shared failover policy.

`AddDecisionTreeExecution()` registers the default `ILlmCompletionPipeline`, a transient default conversation manager, the loader, parser, default classify context builder, in-memory journal, in-memory event publisher, and built-in predicates. `AddAiClevernessLlmClient<T>()` remains the provider-neutral LLM adapter used by the default decision completion pipeline.

Application actions and predicates are registered as singleton catalog entries. Keep their own mutable state out of fields; execution-specific state is passed through their context.

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
      "actionName": "collectEvidence",
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
      "predicateName": "dataExists",
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

Resolve the loader and executor from the provider. Loading validates the JSON against the registered action and predicate catalogs before execution:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

var loader = provider.GetRequiredService<IDecisionTreeLoader>();
var executor = provider.GetRequiredService<DecisionTreeExecutor>();
var cancellationToken = CancellationToken.None;

var json = await File.ReadAllTextAsync("evidence-classification.json");
var tree = loader.Load(json);

var result = await executor.ExecuteAsync(
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

`DecisionTreeResult` contains the execution ID, success flag, terminal verdict, outcome, parsed classifications, final `ResourceUsage`, and an optional error. Possible outcomes are `Terminal`, `Unknown`, `ActionFailed`, `BudgetExhausted`, `Cancelled`, and `ValidationFailed`.

## Implement an action

An action receives the node ID, execution ID, template parameters, mutable execution state, and execution-scoped `DataStore`. It returns a status and may add generic data or string properties:

```csharp
using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

public sealed class CollectEvidenceAction : IDecisionAction
{
    public string Name => "collectEvidence";

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
    public string Name => "hasApprovalFlag";

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

`Halt` returns `DecisionTreeOutcome.BudgetExhausted`. `Warn` continues, and `Throttle` inserts a short delay before continuing. The executor checks cancellation and limits before externally observable work and after recording node visits or LLM usage.

Configure defaults for trees that retain the library's default budget values:

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.DefaultMaxNodeVisits = 30;
    options.DefaultMaxLlmCalls = 5;
    options.DefaultMaxElapsedTime = TimeSpan.FromSeconds(60);
    options.DefaultMaxContextTokens = 2000;
    options.TraceId = "decision-service";
    options.CorrelationId = "request-123";
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = false;
    options.TranscriptRedactor = text => text;
});
```

Prefer setting explicit budgets in each JSON tree when different workflows need different limits.

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

Decision-tree runs can use the same Markdown transcript runtime as agent runs. Configure an absolute directory through `DecisionTreeExecutionOptions`:

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = false;
    options.TranscriptRedactor = text => text;
});
```

Normal mode requires `TranscriptRedactor`; content such as answers, observations, action errors, verdicts, and outcomes is passed through it before persistence. Debug mode is explicitly unredacted and bypasses that requirement:

```csharp
services.AddDecisionTreeExecution(options =>
{
    options.TranscriptDirectory = Path.GetFullPath("transcripts");
    options.TranscriptDebug = true;
});
```

Decision transcripts contain node visits, action completions, classification results, the final decision outcome, and resource usage. Transcript writes are best effort and do not change the decision result. The public `DecisionTreeResult` does not expose a transcript path; applications can inspect the configured directory or consume the public journal/event records.

The demo enables these settings with the existing switches:

```powershell
dotnet run --project AiClevernessLib.Demo -- /t  # normal, redacted transcript
dotnet run --project AiClevernessLib.Demo -- /d  # debug, unredacted transcript
```

The demo prints the Feature 08 transcript path after scenario 8. Files are written under `AiClevernessLib.Demo/bin/Debug/net10.0/transcripts` for a Debug build.

## Current integration boundaries

Decision-tree execution and the ordinary agent tool loop use the same registered `ILlmCompletionPipeline` boundary and shared model-failover handler. Their orchestration flows remain separate, but completion policy, transient-failure classification, fallback advancement, and model-switch notifications are centralized in the shared runtime.

Decision events are available through the public journal and event-bus contracts. Decision transcript output is provided through the opt-in `DecisionTreeExecutionOptions` settings described above; the executor still does not expose the internal transcript sink or context.
