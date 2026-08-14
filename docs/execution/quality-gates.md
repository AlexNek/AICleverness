# Quality Gates

Quality gates evaluate the final result before it is returned. A gate can
approve, reject, request a retry, or provide a replacement result.

## Implementing a Gate

```csharp
public sealed class JsonSchemaGate : IAgentQualityGate
{
    public string Name => "JsonSchema";
    public int Priority => 100;
    public bool AppliesTo(IAgentContext context) => true;

    public Task<QualityGateResult> EvaluateAsync(
        AgentResult result,
        IAgentContext context,
        CancellationToken ct)
    {
        var valid = IsValidJson(result.Output);
        return Task.FromResult(new QualityGateResult(
            Approved: valid,
            Retry: !valid,
            Reason: valid ? null : "Output must be valid JSON."));
    }
}

services.AddAgentQualityGate<JsonSchemaGate>();
```

## Retry Behavior

When a gate sets `Retry`, the runtime feeds the gate's `Reason` back into
the next LLM attempt, so the model can correct the output. Control the retry
budget with the `max_quality_retries` request parameter or the
`DefaultMaxQualityRetries` runtime option.

A gate may also return a `ReplacementResult` to substitute the output
outright instead of retrying.

## Scoping

Gates support [agent-scoped registration](agent-scoping.md) — e.g. a URL
structure gate that only runs for `UrlResearchAgent` executions.
