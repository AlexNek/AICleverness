# Validators and Transformers

After the quality gates, two simple steps run on the final result:

- **Validators** — check the result: is it acceptable? Yes or no.
- **Transformers** — change the result into its final form, for example
  formatting or removing private data.

## Result Validators

```csharp
services.AddAgentResultValidator<MyValidator>();
```

Implement `IAgentResultValidator` and return a `ValidationResult` with
`IsValid` and `Error`. If the validator fails, the whole run is marked as
unsuccessful.

## Result Transformers

```csharp
services.AddAgentResultTransformer<PiiRedactor>();
```

Implement `IAgentResultTransformer` to rewrite the output. Typical uses:
remove personal data, fix the formatting, unify the output format. If you
register several transformers, they run in descending `Priority` order —
the highest `Priority` runs first. When two transformers have the same
`Priority`, they run in the order you registered them.

## Output Guards

For security checks on the output — for example: does the answer contain a
secret, or dangerous content — implement `IOutputGuard` instead. See
[Security and Approval](../security/security-approval.md). The rule of
thumb: guards protect security, transformers shape content.

Both validators and transformers support
[agent-scoped registration](agent-scoping.md).
