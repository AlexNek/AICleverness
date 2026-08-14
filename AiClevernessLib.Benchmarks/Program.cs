using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Benchmarks;

// ── Entry Point ───────────────────────────────────────────────────────────

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

// ── Test Helpers ──────────────────────────────────────────────────────────

public sealed class BenchmarkLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        LlmCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmResponse("Benchmark response."));
    }
}

public sealed class BenchmarkTool : ITool
{
    public ToolDefinition Definition { get; } = new("benchmark-tool", "A tool for benchmarking.");

    public string Description => "A tool for benchmarking.";

    public string Name => "benchmark-tool";

    public Task<ToolResult>
        InvokeAsync(ToolInvocation invocation, CancellationToken ct = default) =>
        Task.FromResult(new ToolResult(true, "ok"));
}

// ── JSON Serialization Benchmarks ─────────────────────────────────────────

[MemoryDiagnoser]
public class JsonBenchmarks
{
    private const string EmptyJson = "{}";

    private const string NullJson = "";

    private const string SampleJson =
        """{"key1":"value1","key2":42,"key3":true,"key4":{"nested":"value"}}""";

    [Benchmark]
    public Dictionary<string, object> ParseEmptyJson()
    {
        return ParseArguments(EmptyJson);
    }

    [Benchmark]
    public Dictionary<string, object> ParseNullJson()
    {
        return ParseArguments(NullJson);
    }

    [Benchmark(Baseline = true)]
    public Dictionary<string, object> ParseSampleJson()
    {
        return ParseArguments(SampleJson);
    }

    [Benchmark]
    public string SerializeWithNewOptions()
    {
        var dict = new Dictionary<string, object> { ["key"] = "value", ["num"] = 42 };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(dict, opts);
    }

    [Benchmark]
    public string SerializeWithSharedOptions()
    {
        var dict = new Dictionary<string, object> { ["key"] = "value", ["num"] = 42 };
        return JsonSerializer.Serialize(dict, AiClevernessJson.Default);
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => ConvertJsonElement(p.Value)),
                _ => element.ToString()
            };
    }

    // Replicate the runtime's ParseArguments for benchmarking.
    private static Dictionary<string, object> ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                result[property.Name] = ConvertJsonElement(property.Value);
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
    }
}

// ── Memory Benchmarks ─────────────────────────────────────────────────────

[MemoryDiagnoser]
public class MemoryBenchmarks
{
    private InMemoryAgentMemory _memory = null!;

    [Benchmark]
    public async Task ContainsCheck()
    {
        await _memory.SaveAsync("contains-key", "value");
        await _memory.ContainsAsync("contains-key");
    }

    [Benchmark(Baseline = true)]
    public async Task SaveAndLoad()
    {
        await _memory.SaveAsync("bench-key", new { Name = "test", Value = 42 });
        var result = await _memory.LoadAsync<object>("bench-key");
    }

    [Benchmark]
    public async Task SaveString()
    {
        await _memory.SaveAsync("str-key", "hello world");
    }

    [GlobalSetup]
    public void Setup()
    {
        _memory = new InMemoryAgentMemory();
    }
}

// ── Startup Analyzer Benchmarks ───────────────────────────────────────────

[MemoryDiagnoser]
public class StartupAnalyzerBenchmarks
{
    private DefaultStartupAnalyzer _analyzer = null!;

    private IServiceProvider _emptySp = null!;

    private IServiceProvider _fullSp = null!;

    [Benchmark]
    public async Task AnalyzeEmptyContainer()
    {
        await _analyzer.AnalyzeAsync(_emptySp);
    }

    [Benchmark(Baseline = true)]
    public async Task AnalyzeFullContainer()
    {
        await _analyzer.AnalyzeAsync(_fullSp);
    }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<BenchmarkLlmClient>();
        services.AddInMemoryCheckpointStore();
        services.AddInMemoryExecutionJournal();
        services.AddInMemoryEventBus();
        services.AddMetricsCollector();
        services.AddDiagnosticCollector();
        services.AddStartupAnalyzer();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new BenchmarkTool());
        _fullSp = sp;

        _emptySp = new ServiceCollection().BuildServiceProvider();
        _analyzer = new DefaultStartupAnalyzer();
    }

    [Benchmark]
    public void ValidateWorkflowDefinition()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "bench-wf",
                               Name = "Benchmark",
                               EntryNodeId = "n1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "n1",
                                                       Name = "Step 1",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = new AgentRequest("test")
                                                   },
                                               new WorkflowNode
                                                   {
                                                       Id = "n2",
                                                       Name = "Step 2",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = new AgentRequest("test"),
                                                       DependsOn = new[] { "n1" }
                                                   },
                                               new WorkflowNode
                                                   {
                                                       Id = "n3",
                                                       Name = "Step 3",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = new AgentRequest("test"),
                                                       DependsOn = new[] { "n2" }
                                                   }
                                           }
                           };
        DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);
    }
}

// ── Runtime Benchmarks ────────────────────────────────────────────────────

[MemoryDiagnoser]
public class RuntimeBenchmarks
{
    private AgentRuntime _runtime = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<BenchmarkLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new BenchmarkTool());
        _runtime = (AgentRuntime)sp.GetRequiredService<IAgentRuntime>();
    }

    [Benchmark(Baseline = true)]
    public async Task SingleTurnRun()
    {
        var request = new AgentRequest("Do a quick task.");
        await _runtime.RunAsync(request);
    }
}
