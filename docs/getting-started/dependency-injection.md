# Dependency Injection

AiCleverness is built for DI (dependency injection). One call —
`AddAiClevernessRuntime()` — registers the runtime, the default tool
executor, the registries, and the in-memory defaults. Everything else is
added only if you call its `Add...` method yourself.

## Full Setup

This example shows all available registrations. In a real application you
only use the ones you need.

```csharp
// Core runtime
services.AddAiClevernessRuntime(options =>
{
    options.DefaultMaxTurns = 10;
    options.DefaultCompletionTimeoutSeconds = 120;
    options.DefaultMaxQualityRetries = 2;
    options.DefaultToolMaxRetries = 1;
    options.EnableModelFailover = true;  // opt-in: failover to next model on timeout
});
services.AddAiClevernessLlmClient<MyLlmClient>();

// Extension points
services.AddAgentPolicy<RateLimitPolicy>();
services.AddAgentStrategy<CachedResultStrategy>();
services.AddAgentQualityGate<JsonSchemaGate>();
services.AddAgentResultValidator<MyValidator>();
services.AddAgentResultTransformer<PiiRedactor>();
services.AddAgentObserver<RuntimeObserver>();

// Planning
services.AddDefaultPlanner();            // or AddSequentialPlanner()
services.AddNamedPlanner<CustomPlanner>();

// Tools
services.AddAgentTool<WeatherTool>();
services.AddAgentTool<SearchTool>();

// Persistence (only if you need it)
services.AddInMemoryCheckpointStore();
services.AddInMemoryExecutionJournal();

// Hosting (only if you need it)
services.AddHostedAgentRuntime(options =>
{
    options.MaxConcurrentExecutions = 4;
    options.GracePeriodSeconds = 30;
});

// Observability (only if you need it)
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();
```

## Logging

When logging is registered in the container, `ILoggerFactory` is
automatically injected into `AgentRuntime`. Internal components create
typed loggers under their own category — no extra wiring needed:

```csharp
// Any app with a DI container (ASP.NET, WPF, console, worker service, etc.)
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});
services.AddAiClevernessRuntime();
services.AddAiClevernessLlmClient<MyLlmClient>();
```

## Manual Construction (No DI Container)

You do not have to use DI. You can create every part yourself with `new`
and put them together:

```csharp
var tools = new ToolRegistry();
tools.Register(new WeatherTool(...));

var runtime = new AgentRuntime(
    new MyLlmClient(...),
    tools,
    new[] { new RateLimitPolicy() },
    new[] { new CachedResultStrategy() },
    new DefaultPlanner(new MyLlmClient(...)));
```

To enable diagnostic logging in manual construction, pass an
`ILoggerFactory`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddDebug();          // Visual Studio Output window
    builder.SetMinimumLevel(LogLevel.Warning);
});

var runtime = new AgentRuntime(
    new MyLlmClient(...),
    tools,
    loggerFactory: loggerFactory);
```

## Change the Settings for One Run

The values you set in `AddAiClevernessRuntime(options => ...)` are the
defaults for all runs. You can change them for a single run through
`AgentRequest.Parameters`. The most common keys:

```csharp
var request = new AgentRequest(
    Goal: "Research the API base URL for provider XYZ",
    AllowedToolNames: ["search_web", "fetch_url"],
    Parameters: new Dictionary<string, object>
    {
        ["system_prompt"] = "You are a URL research specialist.",
        ["max_turns"] = 10,               // max LLM turns
        ["temperature"] = 0.0f,            // 0 = less variation between answers
        ["model"] = "gpt-4o",              // model name for this run
        ["completion_timeout_seconds"] = 120,  // max wait for one LLM call
        ["tool_timeout_seconds"] = 30,         // max wait for one tool call
        ["tool_max_retries"] = 2,          // retry a failed tool call
        ["max_quality_retries"] = 1,       // retries when a quality gate rejects
        ["enable_model_failover"] = true,  // failover to next model on timeout
        ["model_fallback_chain"] = new[] { "gpt-4o", "claude-3.5-sonnet" }  // ordered fallbacks
    });
```

See [Runtime Pipeline](../concepts/runtime-pipeline.md) for what each step
does with these parameters, and
[DI Extensions](../api-reference/di-extensions.md) for the full list of
registration methods.
