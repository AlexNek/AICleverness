# LLM integration

This guide shows how an external LLM orchestration layer can use
`BrowserSession`. The session supplies browser state and executes operations;
the orchestration layer decides what to do next.

## Basic loop pattern

```csharp
using WebTools.NET;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

var options = new BrowserSessionOptions
{
    DefaultFormat = EContentFormat.Markdown,
    IncludeScreenshot = false,
    MaxOperations = 50
};

var sessionFactory = new BrowserSessionFactory(sessionOptions: options);
await using var browser = sessionFactory.Create();
await using var session = new BrowserSession(browser, options);
var snapshot = await session.StartAsync("https://test.example.com/login");

while (true)
{
    // The external orchestration layer presents the snapshot to the LLM.
    var operation = await llm.DecideNextOperationAsync(snapshot);

    if (operation is null)
    {
        break;
    }

    snapshot = await session.ExecuteAsync(operation);

    if (snapshot.Error is not null)
    {
        // Optionally let the LLM see the error and decide recovery.
        continue;
    }
}
```

## What the LLM sees

Each `BrowserSnapshot` gives the caller:

1. **Content** — the page body in Markdown, PlainText, or Html according to
   the configured `EContentFormat`.
2. **Elements** — a numbered list of interactive elements:
   ```text
   [1] a: "Sign In" → /login
   [2] input[text]: placeholder="Email" name="email"
   [3] input[password]: placeholder="Password" name="password"
   [4] button: "Submit"
   ```
3. **Metadata** — URL, title, status, errors, and scroll state.

## Login flow example

```csharp
var snapshot = await session.StartAsync("https://test.example.com/login");

snapshot = await session.ExecuteAsync(new BrowserOperation(
    EBrowserOperationType.FillForm,
    Fields: [
        new FormFieldValue(2, "user@test.example.com"),
        new FormFieldValue(3, "test-password-123")
    ]));

var submitIndex = snapshot.Elements
    .Single(element => element.Tag == "button" && element.Text == "Submit")
    .Index;

snapshot = await session.ExecuteAsync(new BrowserOperation(
    EBrowserOperationType.Click,
    ElementIndex: submitIndex));

// Verify that snapshot.Url is now the expected dashboard URL.
```

## Scroll for lazy content

```csharp
var snapshot = await session.StartAsync("https://test.example.com/feed");

while (true)
{
    ProcessContent(snapshot.Content);

    if (!snapshot.HasMoreContent)
    {
        break;
    }

    snapshot = await session.ExecuteAsync(
        new BrowserOperation(EBrowserOperationType.ScrollDown));
}
```

## Formatting the prompt

A typical system prompt for the external LLM can describe the operation
contract as follows:

```text
You receive a BrowserSnapshot with page content and numbered interactive elements.

To interact, respond with a JSON BrowserOperation:
- Navigate: {"Type": "Navigate", "Value": "https://..."}
- Click:    {"Type": "Click", "ElementIndex": 3}
- Fill:     {"Type": "Fill", "ElementIndex": 2, "Value": "hello"}
- FillForm: {"Type": "FillForm", "Fields": [{"ElementIndex": 2, "Value": "x"}]}
- Submit:   {"Type": "Submit", "ElementIndex": 4}
- Scroll:   {"Type": "ScrollDown"}
- Done:     null (when the task is complete)

If Error is present in the snapshot, adjust your approach.
```

## Operation history

The session tracks operations for the current workflow:

```csharp
var history = session.OperationHistory;
// Pass it to the LLM as conversation context if useful.
```

## Error recovery

The external caller can recover from errors because the session remains
available:

| Error | Recovery strategy |
| --- | --- |
| `Element index N not found` | Re-read elements and choose another index |
| `HTTP 404` | Try another URL |
| `Timeout` | Try `ScrollDown` or `WaitFor` |
| Operation limit reached | Finish the workflow or create a new session |

## Tips

- Use `EContentFormat.Markdown` for a good structure/size balance.
- Set `IncludeScreenshot = true` only for multimodal consumers.
- Use `StorageStatePath` for authentication across workflows only when the state file is protected and excluded from source control. Persisted state may contain authentication cookies; use a separate path for each independent workflow or identity and never share it between unrelated or concurrent workflows.
- Keep `MaxOperations` reasonable to prevent runaway loops.
- A session is not thread-safe; use one session per workflow thread.
