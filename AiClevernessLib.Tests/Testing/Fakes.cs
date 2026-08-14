using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

/// <summary>
/// Fake <see cref="AiCleverness.Abstractions.IToolExecutor"/> that records all executions
/// and returns configurable results. Useful for testing middleware and runtime
/// without actually invoking tools.
/// </summary>
public sealed class FakeToolExecutor : AiCleverness.Abstractions.IToolExecutor
{
    private readonly List<FakeToolExecutionRecord> _executions = [];

    private readonly Queue<ToolResult> _results = new();

    private ToolResult? _defaultResult;

    /// <summary>Number of tool executions.</summary>
    public int ExecutionCount => _executions.Count;

    /// <summary>All tool executions, in order.</summary>
    public IReadOnlyList<FakeToolExecutionRecord> Executions => _executions;

    /// <summary>
    /// Queues a failed result with the given error.
    /// </summary>
    public FakeToolExecutor EnqueueFailure(string error = "Tool failed")
    {
        _results.Enqueue(new ToolResult(false, null, error));
        return this;
    }

    /// <summary>
    /// Queues a result to be returned on the next execution.
    /// </summary>
    public FakeToolExecutor EnqueueResult(ToolResult result)
    {
        _results.Enqueue(result);
        return this;
    }

    /// <summary>
    /// Queues a successful result with the given output.
    /// </summary>
    public FakeToolExecutor EnqueueSuccess(string output = "ok")
    {
        _results.Enqueue(new ToolResult(true, output));
        return this;
    }

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(
        AiCleverness.Abstractions.ITool tool,
        ToolInvocation invocation,
        ToolExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        var result = _results.Count > 0 ? _results.Dequeue() : _defaultResult;
        if (result is null)
        {
            result = new ToolResult(true, $"FakeToolExecutor: {tool.Name} executed");
        }

        _executions.Add(new FakeToolExecutionRecord(tool.Name, invocation, policy, result));
        return Task.FromResult(result);
    }

    /// <summary>
    /// Clears queued results and execution history.
    /// </summary>
    public FakeToolExecutor Reset()
    {
        _results.Clear();
        _defaultResult = null;
        _executions.Clear();
        return this;
    }

    /// <summary>
    /// Sets the default result returned when the queue is empty.
    /// </summary>
    public FakeToolExecutor SetDefaultResult(ToolResult result)
    {
        _defaultResult = result;
        return this;
    }

    /// <summary>
    /// Sets a default success result.
    /// </summary>
    public FakeToolExecutor SetDefaultSuccess(string output = "ok")
    {
        _defaultResult = new ToolResult(true, output);
        return this;
    }
}

/// <summary>
/// Record of a single tool execution through <see cref="FakeToolExecutor"/>.
/// </summary>
public sealed record FakeToolExecutionRecord(
    string ToolName,
    ToolInvocation Invocation,
    ToolExecutionPolicy Policy,
    ToolResult Result);

/// <summary>
/// Fake <see cref="AiCleverness.Abstractions.IAgentPlanner"/> that returns a configurable plan.
/// </summary>
public sealed class FakePlanner : AiCleverness.Abstractions.IAgentPlanner
{
    private readonly List<PlannedStep> _steps;

    private int _planCallCount;

    /// <summary>Number of times PlanAsync was called.</summary>
    public int PlanCallCount => _planCallCount;

    /// <summary>
    /// Creates a planner that returns the given steps.
    /// </summary>
    public FakePlanner(IEnumerable<PlannedStep>? steps = null)
    {
        _steps = steps?.ToList() ?? [];
    }

    /// <summary>
    /// Creates a planner that returns an empty plan.
    /// </summary>
    public static FakePlanner Empty() => new();

    /// <inheritdoc />
    public Task<IReadOnlyList<PlannedStep>> PlanAsync(
        AgentRequest request,
        AiCleverness.Abstractions.IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        _planCallCount++;
        return Task.FromResult<IReadOnlyList<PlannedStep>>(_steps.AsReadOnly());
    }

    /// <summary>
    /// Creates a planner with simple string steps.
    /// </summary>
    public static FakePlanner WithSteps(params string[] descriptions)
    {
        var steps = descriptions.Select((d, i) => new PlannedStep($"step-{i + 1}", "action", d))
            .ToList();
        return new FakePlanner(steps);
    }
}

/// <summary>
/// Fake <see cref="AiCleverness.Abstractions.IAgentMemory"/> backed by a simple dictionary.
/// Tracks all operations for assertion.
/// </summary>
public sealed class FakeMemory : AiCleverness.Abstractions.IAgentMemory
{
    private readonly List<string> _operations = [];

    private readonly Dictionary<string, object> _store = new();

    /// <summary>Number of items currently stored.</summary>
    public int Count => _store.Count;

    /// <summary>All operations performed on this memory, in order.</summary>
    public IReadOnlyList<string> Operations => _operations;

    /// <inheritdoc />
    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
    {
        _operations.Add($"CONTAINS:{key}");
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        _operations.Add("KEYS");
        return Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToList().AsReadOnly());
    }

    /// <inheritdoc />
    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _operations.Add($"LOAD:{key}");
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    /// <summary>
    /// Clears all data and operation history.
    /// </summary>
    public FakeMemory Reset()
    {
        _store.Clear();
        _operations.Clear();
        return this;
    }

    /// <inheritdoc />
    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _store[key] = value!;
        _operations.Add($"SAVE:{key}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pre-populates the memory with a value.
    /// </summary>
    public FakeMemory WithValue(string key, object value)
    {
        _store[key] = value;
        return this;
    }
}

/// <summary>
/// Controllable clock for testing time-dependent behavior.
/// Provides a fixed or advancing <see cref="DateTimeOffset"/>.
/// </summary>
public sealed class FakeClock
{
    private DateTimeOffset _now;

    /// <summary>Current time according to this clock.</summary>
    public DateTimeOffset UtcNow => _now;

    /// <summary>
    /// Creates a clock set to the specified time.
    /// </summary>
    public FakeClock(DateTimeOffset initialTime)
    {
        _now = initialTime;
    }

    /// <summary>
    /// Creates a clock set to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public FakeClock()
        : this(DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    public FakeClock Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
        return this;
    }

    /// <summary>
    /// Advances the clock by the specified number of seconds.
    /// </summary>
    public FakeClock AdvanceSeconds(int seconds)
    {
        _now = _now.AddSeconds(seconds);
        return this;
    }

    /// <summary>
    /// Sets the clock to a specific time.
    /// </summary>
    public FakeClock SetTo(DateTimeOffset time)
    {
        _now = time;
        return this;
    }
}
