# Dependency Injection

AiCleverness is DI-first. `AddAiClevernessRuntime()` registers the runtime,
default executor, registries, and in-memory defaults; every other concern is
opt-in.

## Full Setup

```csharp
// Core runtime
services.AddAiClevernessRuntime(options =>
{
    options.DefaultMaxTurns = 10;
    options.DefaultCompletionTimeoutSeconds = 120;
    options.DefaultMaxQualityRetries = 2;
    options.DefaultToolMaxRetries = 1;
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

// Persistence (opt-in)
services.AddInMemoryCheckpointStore();
services.AddInMemoryExecutionJournal();

// Hosting (opt-in)
services.AddHostedAgentRuntime(options =>
{
    options.MaxConcurrentExecutions = 4;
    options.GracePeriodSeconds = 30;
});

// Observability (opt-in)
services.AddMetricsCollector();
services.AddDiagnosticCollector();
services.AddStartupAnalyzer();
services.AddOpenTelemetryObserver();
```

## Without DI

All runtime pieces are constructible directly:

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

## Per-Request Tuning

Runtime defaults can be overridden per request through `AgentRequest.Parameters`:

```csharp
var request = new AgentRequest(
    Goal: "Research the API base URL for provider XYZ",
    AllowedToolNames: ["search_web", "fetch_url"],
    Parameters: new Dictionary<string, object>
    {
        ["system_prompt"] = "You are a URL research specialist.",
        ["max_turns"] = 10,
        ["temperature"] = 0.0f,
        ["model"] = "gpt-4o",
        ["completion_timeout_seconds"] = 120,
        ["tool_timeout_seconds"] = 30,
        ["tool_max_retries"] = 2,
        ["max_quality_retries"] = 1
    });
```

See [Runtime Pipeline](../concepts/runtime-pipeline.md) for what each stage
does with these parameters, and
[DI Extensions](../api-reference/di-extensions.md) for the full list of
registration methods.
