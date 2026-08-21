# BrowserSession

`BrowserSession` is a stateful browser-session capability for applications that
need to navigate, interact with, and extract information from web pages across
multiple turns. The caller may be a console application, workflow, or LLM
orchestration layer. WebTools.NET does not choose operations or contain a
decision loop.

## Architecture

```
┌─────────────────────────────────────────────────┐
│  External caller                                │
│  (console, workflow, LLM orchestration, etc.)   │
│  decides operations                             │
└──────────────────┬──────────────────────────────┘
                   │ BrowserOperation
                   ▼
┌─────────────────────────────────────────────────┐
│  BrowserSession                                 │
│  - Maintains session state                      │
│  - Executes operations                          │
│  - Returns BrowserSnapshot after each operation │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  IBrowserSession (Playwright/CloakBrowser)     │
│  - Navigate, Click, Fill, Scroll, etc.          │
└─────────────────────────────────────────────────┘
```

## Quick Start

```csharp
using WebTools.NET;
using WebTools.NET.Browsing;
using WebTools.NET.Models;

var sessionFactory = new BrowserSessionFactory();
await using var browser = sessionFactory.Create();
await using var session = new BrowserSession(browser);

var snapshot = await session.StartAsync("https://test.example.com");

var operation = new BrowserOperation(
    EBrowserOperationType.Click,
    ElementIndex: 3);
snapshot = await session.ExecuteAsync(operation);
```

## Construction and ownership

Create a fresh external browser session for each independent workflow and pass
it into the non-owning `BrowserSession`. `BrowserSession` never creates,
disposes, or replaces the supplied session.

```csharp
var options = new BrowserSessionOptions
{
    MaxOperations = 100,
    MaxDuration = TimeSpan.FromMinutes(10),
    IncludeScreenshot = true,
    StorageStatePath = "./cookies.json",
    ViewportHeight = 720
};

var sessionFactory = new BrowserSessionFactory(
    storageStatePath: options.StorageStatePath,
    sessionOptions: options);
await using var browser = sessionFactory.Create();
await using var session = new BrowserSession(browser, options);
```

For concurrent or unrelated tasks, call `Create()` once per task. Never share
one browser session or one `BrowserSession` between independent workflows.

## Operation vocabulary

| Operation | Purpose | Required fields |
| --- | --- | --- |
| `Navigate` | Go to a URL | `Value` = URL |
| `Click` | Click element by index | `ElementIndex` |
| `Fill` | Fill input by index | `ElementIndex`, `Value` |
| `FillForm` | Fill multiple fields at once | `Fields` (array of `FormFieldValue`) |
| `Select` | Select dropdown option | `ElementIndex`, `Value` = option text |
| `Submit` | Submit form containing element | `ElementIndex` |
| `ScrollDown` | Scroll one viewport down | — |
| `ScrollUp` | Scroll one viewport up | — |
| `WaitFor` | Wait for CSS selector to appear | `Value` = selector, optional `TimeoutMs` |
| `Back` | Browser back button | — |
| `Snapshot` | Re-read page without interaction | — |

## BrowserSnapshot

After startup and every operation, the session returns a `BrowserSnapshot`:

| Field | Description |
| --- | --- |
| `Url` | Current page URL after redirects |
| `Title` | Page title |
| `Content` | Page content formatted per chosen `EContentFormat` |
| `Elements` | Interactive links, buttons, inputs, checkboxes, and selects |
| `Format` | Content format used |
| `StatusCode` | HTTP status of last navigation |
| `Error` | Error description on failure, null on success |
| `HasMoreContent` | True if page has more content below the current scroll |
| `ScreenshotBase64` | Base64 PNG when `IncludeScreenshot` is enabled |

## Element indexing

Elements are numbered 1..N in each snapshot. The external caller refers to them
by index. After each operation, elements are re-extracted and re-indexed;
indices are ephemeral and not stable across turns.

## Error handling

Failed operations return a snapshot with `Error` populated where possible, and
the session remains available for the next operation:

```csharp
var snapshot = await session.ExecuteAsync(
    new BrowserOperation(EBrowserOperationType.Click, ElementIndex: 99));

if (snapshot.Error is not null)
{
    // "Element index 99 not found"
    // The session is still available for recovery.
}
```

## Safety limits

| Option | Default | Description |
| --- | --- | --- |
| `MaxOperations` | 50 | Maximum operations per session |
| `MaxDuration` | 5 minutes | Maximum session wall-clock time |

When a limit is reached, `ExecuteAsync` returns the last known snapshot with an
error and refuses further operations. Built-in browser sessions reset their
page/context when a duration deadline interrupts an in-flight operation.
External sessions that do not implement `IBrowserSessionLifecycle` must honor
cancellation cooperatively; otherwise the session preserves serialization by
waiting for the operation to finish.

## Cookie persistence

Set `StorageStatePath` to persist cookies across workflows:

```csharp
var options = new BrowserSessionOptions
{
    StorageStatePath = "./browser-state.json"
};

var sessionFactory = new BrowserSessionFactory(
    storageStatePath: options.StorageStatePath,
    sessionOptions: options);
await using var browser = sessionFactory.Create();
await using var session = new BrowserSession(browser, options);

var snapshot = await session.StartAsync("https://test.example.com/dashboard");
```

## Lifetime

Dispose `BrowserSession` and then the explicitly created browser session with
`await using`. `BrowserSession` saves configured storage state but does not own
the supplied browser session. Declaring the browser before the wrapper ensures
reverse-order disposal closes the wrapper before its browser session.
