# Quality Gates

A quality gate checks the model's answer before it goes back to you. A gate
has four options:

- **Approve** the answer — the run continues.
- **Reject with a retry** — the runtime sends the reason back to the model
  and tries again.
- **Replace** the answer — the gate returns its own result instead.
- **Reject without a retry** — the run fails.

## Implementing a Gate

This example checks that the answer is valid JSON:

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

The gate returns a `QualityGateResult` with three core fields: `Approved`
(is the answer acceptable?), `Retry` (should the model try again?), and
`Reason` (the explanation for the model).

## Retry Behavior

When a gate sets `Retry = true`, the runtime sends the gate's `Reason` to
the model with the next attempt. This tells the model what to fix. The
number of retries is limited by the `max_quality_retries` request parameter
or by the `DefaultMaxQualityRetries` runtime option.

A gate can also return a `ReplacementResult`. Then its own output replaces
the model's answer, and there is no retry.

## Scoping

Gates support [agent-scoped registration](agent-scoping.md). For example, a
URL structure gate can run only for runs of the `UrlResearchAgent`.
