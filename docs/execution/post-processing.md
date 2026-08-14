# Validators and Transformers

Two lightweight post-processing stages run after the quality gates:

- **Validators** — simple pass/fail checks on the final result
- **Transformers** — ordered final formatting and redaction

## Result Validators

```csharp
services.AddAgentResultValidator<MyValidator>();
```

Implement `IAgentResultValidator` and return a `ValidationResult`
(`IsValid`, `Error`). A failed validation marks the run unsuccessful.

## Result Transformers

```csharp
services.AddAgentResultTransformer<PiiRedactor>();
```

Implement `IAgentResultTransformer` to rewrite the output — PII redaction,
formatting, normalization. Transformers run in registration order.

## Output Guards

For security-focused output checks (secret leakage, unsafe content),
implement `IOutputGuard` instead — see
[Security and Approval](../security/security-approval.md). Guards are the
security boundary; transformers are for shaping content.

Both validators and transformers support
[agent-scoped registration](agent-scoping.md).
