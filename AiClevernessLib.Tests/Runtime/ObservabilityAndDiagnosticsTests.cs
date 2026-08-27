using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiClevernessLib.Tests.Runtime;

public class ObservabilityAndDiagnosticsTests
{
    // ── HealthCheck ────────────────────────────────────────────────────────

    [Fact]
    public async Task AiClevernessHealthCheck_NoCoordinator_Healthy()
    {
        var hc = new AiClevernessHealthCheck();

        var result = await hc.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task AiClevernessHealthCheck_ShuttingDown_Unhealthy()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);
        await coordinator.ShutdownAsync("test", TimeSpan.FromSeconds(1));

        var hc = new AiClevernessHealthCheck(coordinator);
        var result = await hc.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task DefaultDiagnosticCollector_Clear_RemovesForExecution()
    {
        var collector = new DefaultDiagnosticCollector();
        await collector.RecordAsync(
            DiagnosticEntry.Info("exec-1", DiagnosticCategory.Runtime, "r", "a"));
        await collector.RecordAsync(
            DiagnosticEntry.Info("exec-2", DiagnosticCategory.Runtime, "r", "b"));

        await collector.ClearAsync("exec-1");

        var report = await collector.GetReportAsync("exec-1");
        report.Entries.Should().BeEmpty();

        var all = await collector.GetAllAsync();
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task DefaultDiagnosticCollector_EmptyReport_ReturnsEmpty()
    {
        var collector = new DefaultDiagnosticCollector();
        var report = await collector.GetReportAsync("nonexistent");
        report.Entries.Should().BeEmpty();
        report.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task DefaultDiagnosticCollector_GetAll_ReturnsAcrossExecutions()
    {
        var collector = new DefaultDiagnosticCollector();
        await collector.RecordAsync(
            DiagnosticEntry.Info("exec-1", DiagnosticCategory.Runtime, "r", "a"));
        await collector.RecordAsync(
            DiagnosticEntry.Info("exec-2", DiagnosticCategory.Runtime, "r", "b"));

        var all = await collector.GetAllAsync();
        all.Should().HaveCount(2);
    }

    // ── DefaultDiagnosticCollector ─────────────────────────────────────────

    [Fact]
    public async Task DefaultDiagnosticCollector_RecordAndRetrieve()
    {
        var collector = new DefaultDiagnosticCollector();
        var entry = DiagnosticEntry.Info(
            "exec-1",
            DiagnosticCategory.ModelSelection,
            "resolver",
            "Selected model");

        await collector.RecordAsync(entry);

        var report = await collector.GetReportAsync("exec-1");
        report.Entries.Should().HaveCount(1);
        report.Entries[0].Message.Should().Be("Selected model");
    }

    // ── DefaultMetricsCollector ────────────────────────────────────────────

    [Fact]
    public async Task DefaultMetricsCollector_EmptyCollection_ReturnsZeroMetrics()
    {
        var collector = new DefaultMetricsCollector();

        var metrics = await collector.GetAggregateMetricsAsync();

        metrics.TotalExecutions.Should().Be(0);
        metrics.AverageDuration.Should().BeNull();
    }

    [Fact]
    public async Task DefaultMetricsCollector_GetExecutionMetrics_NonExistent_ReturnsNull()
    {
        var collector = new DefaultMetricsCollector();
        var m = await collector.GetExecutionMetricsAsync("nonexistent");
        m.Should().BeNull();
    }

    [Fact]
    public async Task DefaultMetricsCollector_GetExecutionMetrics_ReturnsSingleExecution()
    {
        var collector = new DefaultMetricsCollector();
        var manifest = CreateManifest("exec-1", ExecutionStatus.Completed, TimeSpan.FromSeconds(3));
        await collector.RecordAsync(manifest);

        var m = await collector.GetExecutionMetricsAsync("exec-1");

        m.Should().NotBeNull();
        m!.ExecutionId.Should().Be("exec-1");
        m.TotalExecutions.Should().Be(1);
    }

    [Fact]
    public async Task DefaultMetricsCollector_GetToolMetrics_ReturnsBreakdown()
    {
        var collector = new DefaultMetricsCollector();
        var manifest = CreateManifest(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(1),
            events: new ExecutionEvent[]
                        {
                            new ToolCompletedEvent(
                                "exec-1",
                                "search",
                                new ToolResult(true),
                                TimeSpan.FromMilliseconds(100)),
                            new ToolCompletedEvent(
                                "exec-1",
                                "search",
                                new ToolResult(true),
                                TimeSpan.FromMilliseconds(200)),
                            new ToolCompletedEvent(
                                "exec-1",
                                "calc",
                                new ToolResult(false, Error: "err"),
                                TimeSpan.FromMilliseconds(50))
                        });
        await collector.RecordAsync(manifest);

        var tools = await collector.GetToolMetricsAsync();

        tools.Should().HaveCount(2);
        var search = tools.First(t => t.ToolName == "search");
        search.InvocationCount.Should().Be(2);
        search.FailureCount.Should().Be(0);

        var calc = tools.First(t => t.ToolName == "calc");
        calc.InvocationCount.Should().Be(1);
        calc.FailureCount.Should().Be(1);
    }

    [Fact]
    public async Task DefaultMetricsCollector_RecordAndAggregate_ComputesCorrectly()
    {
        var collector = new DefaultMetricsCollector();

        var manifest1 = CreateManifest(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(2),
            events: new ExecutionEvent[]
                        {
                            new LlmRespondedEvent(
                                "exec-1",
                                new LlmResponse("hello", Usage: new LlmTokenUsage(10, 5)),
                                TimeSpan.FromMilliseconds(500)),
                            new ToolInvokedEvent(
                                "exec-1",
                                "tool-a",
                                new ToolInvocation("tool-a", new Dictionary<string, object>())),
                            new ToolCompletedEvent(
                                "exec-1",
                                "tool-a",
                                new ToolResult(true, "ok"),
                                TimeSpan.FromMilliseconds(100))
                        });

        var manifest2 = CreateManifest(
            "exec-2",
            ExecutionStatus.Failed,
            TimeSpan.FromSeconds(5),
            events: new ExecutionEvent[]
                        {
                            new LlmRespondedEvent(
                                "exec-2",
                                new LlmResponse("err", Usage: new LlmTokenUsage(20, 10)),
                                TimeSpan.FromMilliseconds(800)),
                            new ToolInvokedEvent(
                                "exec-2",
                                "tool-b",
                                new ToolInvocation("tool-b", new Dictionary<string, object>())),
                            new ToolCompletedEvent(
                                "exec-2",
                                "tool-b",
                                new ToolResult(false, Error: "fail"),
                                TimeSpan.FromMilliseconds(200)),
                            new QualityGateRejectedEvent(
                                "exec-2",
                                "gate-1",
                                new QualityGateResult(false, Reason: "bad"),
                                1)
                        });

        await collector.RecordAsync(manifest1);
        await collector.RecordAsync(manifest2);

        var agg = await collector.GetAggregateMetricsAsync();

        agg.TotalExecutions.Should().Be(2);
        agg.SuccessfulExecutions.Should().Be(1);
        agg.FailedExecutions.Should().Be(1);
        agg.TotalLlmCalls.Should().Be(2);
        agg.TotalToolInvocations.Should().Be(2);
        agg.FailedToolInvocations.Should().Be(1);
        agg.TotalQualityGateEvaluations.Should().Be(1);
        agg.QualityGateRejections.Should().Be(1);
        agg.AverageDuration.Should().NotBeNull();
    }

    [Fact]
    public async Task DefaultMetricsCollector_Reset_ClearsAllData()
    {
        var collector = new DefaultMetricsCollector();
        await collector.RecordAsync(
            CreateManifest("exec-1", ExecutionStatus.Completed, TimeSpan.FromSeconds(1)));

        await collector.ResetAsync();

        var m = await collector.GetAggregateMetricsAsync();
        m.TotalExecutions.Should().Be(0);
    }

    [Fact]
    public async Task DefaultMetricsCollector_CountsFailedLlmAttempts()
    {
        // Arrange: one success, one timeout, one success after failover —
        // two logical turns, three attempts.
        var collector = new DefaultMetricsCollector();
        await collector.RecordAsync(CreateManifest(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(2),
            events:
            [
                new LlmRespondedEvent(
                    "exec-1",
                    new LlmResponse(null),
                    TimeSpan.FromMilliseconds(500),
                    Turn: 0),
                new LlmFailedEvent(
                    "exec-1",
                    "LLM completion timed out after 1s on turn 0",
                    TimeSpan.FromSeconds(1),
                    Turn: 0),
                new LlmRespondedEvent(
                    "exec-1",
                    new LlmResponse("ok"),
                    TimeSpan.FromMilliseconds(300),
                    Turn: 1)
            ]));

        // Act
        var agg = await collector.GetAggregateMetricsAsync();
        var single = await collector.GetExecutionMetricsAsync("exec-1");

        // Assert — failed attempts are included in the LLM attempt metrics.
        agg.TotalLlmCalls.Should().Be(3);
        agg.AverageLlmDuration.Should().NotBeNull();
        single.Should().NotBeNull();
        single!.TotalLlmCalls.Should().Be(3);
    }

    // ── StartupAnalysis ────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultStartupAnalyzer_EmptyContainer_ReportsErrors()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.IsHealthy.Should().BeFalse();
        result.ErrorCount.Should().BeGreaterThan(0);
        result.Findings.Should().Contain(f => f.ServiceName == "IAgentRuntime");
    }

    [Fact]
    public async Task DefaultStartupAnalyzer_WithCoreServices_NoErrors()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();

        var analyzer = new DefaultStartupAnalyzer();
        var result = await analyzer.AnalyzeAsync(sp);

        result.ErrorCount.Should().Be(0);
        result.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void DiagnosticReport_ByCategory_GroupsCorrectly()
    {
        var report = new DiagnosticReport
                         {
                             ExecutionId = "exec-1",
                             Entries = new[]
                                           {
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.ModelSelection,
                                                   "a",
                                                   "1"),
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.ModelSelection,
                                                   "a",
                                                   "2"),
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.ToolSelection,
                                                   "b",
                                                   "3")
                                           }
                         };

        var grouped = report.ByCategory();
        grouped.Should().ContainKey(DiagnosticCategory.ModelSelection);
        grouped[DiagnosticCategory.ModelSelection].Should().HaveCount(2);
        grouped[DiagnosticCategory.ToolSelection].Should().HaveCount(1);
    }

    // ── DiagnosticReport ───────────────────────────────────────────────────

    [Fact]
    public void DiagnosticReport_Counts_AreCorrect()
    {
        var report = new DiagnosticReport
                         {
                             ExecutionId = "exec-1",
                             Entries = new[]
                                           {
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.ModelSelection,
                                                   "cap-resolver",
                                                   "Selected GPT-4"),
                                               DiagnosticEntry.Warn(
                                                   "exec-1",
                                                   DiagnosticCategory.ToolSelection,
                                                   "tool-reg",
                                                   "Fallback tool used"),
                                               DiagnosticEntry.Error(
                                                   "exec-1",
                                                   DiagnosticCategory.Policy,
                                                   "policy-1",
                                                   "Policy blocked"),
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.Strategy,
                                                   "strategy-1",
                                                   "Cache hit")
                                           }
                         };

        report.InfoCount.Should().Be(2);
        report.WarningCount.Should().Be(1);
        report.ErrorCount.Should().Be(1);
        report.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void DiagnosticReport_GetBySeverity_FiltersCorrectly()
    {
        var report = new DiagnosticReport
                         {
                             ExecutionId = "exec-1",
                             Entries = new[]
                                           {
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.Runtime,
                                                   "r",
                                                   "info"),
                                               DiagnosticEntry.Warn(
                                                   "exec-1",
                                                   DiagnosticCategory.Runtime,
                                                   "r",
                                                   "warn"),
                                               DiagnosticEntry.Error(
                                                   "exec-1",
                                                   DiagnosticCategory.Runtime,
                                                   "r",
                                                   "error")
                                           }
                         };

        report.GetBySeverity(DiagnosticSeverity.Warning).Should().HaveCount(1);
        report.GetBySeverity(DiagnosticSeverity.Error).Should().HaveCount(1);
    }

    [Fact]
    public void DiagnosticReport_NoIssues_HasIssuesIsFalse()
    {
        var report = new DiagnosticReport
                         {
                             ExecutionId = "exec-1",
                             Entries = new[]
                                           {
                                               DiagnosticEntry.Info(
                                                   "exec-1",
                                                   DiagnosticCategory.Runtime,
                                                   "runtime",
                                                   "ok")
                                           }
                         };

        report.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void ExecutionGraph_EmptyEvents_HasStartAndEnd()
    {
        var graph = ExecutionGraph.FromEvents(
            "exec-1",
            ExecutionStatus.Cancelled,
            null,
            Array.Empty<ExecutionEvent>());

        graph.Nodes.Should().HaveCount(2);
        graph.Nodes[0].Type.Should().Be(ExecutionGraphNodeType.Start);
        graph.Nodes[1].Type.Should().Be(ExecutionGraphNodeType.End);
        graph.Edges.Should().HaveCount(1);
    }

    // ── ExecutionGraph ─────────────────────────────────────────────────────

    [Fact]
    public void ExecutionGraph_FromEvents_CreatesNodesAndEdges()
    {
        var events = new ExecutionEvent[]
                         {
                             new ExecutionStartedEvent("exec-1", new AgentRequest("test")),
                             new LlmCalledEvent("exec-1", Array.Empty<LlmMessage>()),
                             new LlmRespondedEvent(
                                 "exec-1",
                                 new LlmResponse("hello"),
                                 TimeSpan.FromMilliseconds(500)),
                             new ToolInvokedEvent(
                                 "exec-1",
                                 "search",
                                 new ToolInvocation("search", new Dictionary<string, object>())),
                             new ToolCompletedEvent(
                                 "exec-1",
                                 "search",
                                 new ToolResult(true, "ok"),
                                 TimeSpan.FromMilliseconds(100)),
                             new ExecutionCompletedEvent(
                                 "exec-1",
                                 new AgentResult(true, "output"),
                                 TimeSpan.FromSeconds(2))
                         };

        var graph = ExecutionGraph.FromEvents(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(2),
            events);

        graph.ExecutionId.Should().Be("exec-1");
        graph.Nodes.Should().HaveCountGreaterThan(2);
        graph.Nodes[0].Type.Should().Be(ExecutionGraphNodeType.Start);
        graph.Nodes[^1].Type.Should().Be(ExecutionGraphNodeType.End);
        graph.Edges.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ExecutionGraph_FromEvents_ProjectsClassificationCompletion()
    {
        var events = new ExecutionEvent[]
        {
            new DecisionClassificationCompletedEvent(
                "exec-1",
                "classify",
                "supported",
                "evidence",
                "high",
                1,
                TimestampOverride: DateTimeOffset.UnixEpoch)
        };

        var graph = ExecutionGraph.FromEvents(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(1),
            events);

        graph.Nodes.Should().Contain(node =>
            node.Type == ExecutionGraphNodeType.DecisionNode
            && node.Label == "Decision classification: supported");
    }

    [Fact]
    public void ExecutionGraph_ToMermaid_ProducesValidOutput()
    {
        var events = new ExecutionEvent[]
                         {
                             new ExecutionStartedEvent("exec-1", new AgentRequest("test")),
                             new LlmCalledEvent("exec-1", Array.Empty<LlmMessage>()),
                             new ExecutionCompletedEvent(
                                 "exec-1",
                                 new AgentResult(true),
                                 TimeSpan.FromSeconds(1))
                         };

        var graph = ExecutionGraph.FromEvents(
            "exec-1",
            ExecutionStatus.Completed,
            TimeSpan.FromSeconds(1),
            events);
        var mermaid = graph.ToMermaid();

        mermaid.Should().Contain("graph TB");
        mermaid.Should().Contain("start");
        mermaid.Should().Contain("-->");
    }

    [Fact]
    public void ExecutionMetrics_DerivedRates_ComputeCorrectly()
    {
        var metrics = new ExecutionMetrics
                          {
                              TotalExecutions = 10,
                              SuccessfulExecutions = 8,
                              FailedExecutions = 2,
                              TotalQualityGateEvaluations = 5,
                              QualityGateRejections = 1,
                              TotalToolInvocations = 20,
                              FailedToolInvocations = 4,
                              TotalPromptTokens = 100,
                              TotalCompletionTokens = 50
                          };

        metrics.SuccessRate.Should().Be(0.8);
        metrics.QualityGatePassRate.Should().Be(0.8);
        metrics.ToolFailureRate.Should().Be(0.2);
        metrics.TotalTokens.Should().Be(150);
    }
    // ── ExecutionMetrics ───────────────────────────────────────────────────

    [Fact]
    public void ExecutionMetrics_DerivedRates_WithZeroExecutions_ReturnsNull()
    {
        var metrics = new ExecutionMetrics();

        metrics.SuccessRate.Should().BeNull();
        metrics.QualityGatePassRate.Should().BeNull();
        metrics.ToolFailureRate.Should().BeNull();
        metrics.TotalTokens.Should().Be(0);
    }

    // ── RuntimeHealthStatus ────────────────────────────────────────────────

    [Fact]
    public void RuntimeHealthStatus_Healthy_Defaults()
    {
        var status = new RuntimeHealthStatus();

        status.Status.Should().Be(HealthState.Healthy);
        status.IsHealthy.Should().BeTrue();
        status.Components.Should().BeEmpty();
    }

    [Fact]
    public void RuntimeHealthStatus_Unhealthy_WhenComponentFails()
    {
        var status = new RuntimeHealthStatus
                         {
                             Status = HealthState.Unhealthy,
                             Components = new[]
                                              {
                                                  new ComponentHealthEntry(
                                                      "runtime",
                                                      HealthState.Healthy),
                                                  new ComponentHealthEntry(
                                                      "journal",
                                                      HealthState.Unhealthy,
                                                      "Disk full")
                                              }
                         };

        status.IsHealthy.Should().BeFalse();
        status.Components.Should().HaveCount(2);
    }

    [Fact]
    public void StartupAnalysisResult_CountsAreCorrect()
    {
        var result = new StartupAnalysisResult
                         {
                             Findings = new[]
                                            {
                                                new StartupFinding(
                                                    "A",
                                                    StartupSeverity.Error,
                                                    "missing"),
                                                new StartupFinding(
                                                    "B",
                                                    StartupSeverity.Warning,
                                                    "recommended"),
                                                new StartupFinding(
                                                    "C",
                                                    StartupSeverity.Info,
                                                    "present"),
                                                new StartupFinding(
                                                    "D",
                                                    StartupSeverity.Info,
                                                    "present")
                                            }
                         };

        result.ErrorCount.Should().Be(1);
        result.WarningCount.Should().Be(1);
        result.InfoCount.Should().Be(2);
        result.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void ToolMetrics_FailureRate_Computes()
    {
        var m = new ToolMetrics(
            "web-search",
            10,
            3,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500));
        m.FailureRate.Should().Be(0.3);
    }

    [Fact]
    public void ToolMetrics_FailureRate_ZeroInvocations_ReturnsNull()
    {
        var m = new ToolMetrics("tool", 0, 0, null, null);
        m.FailureRate.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ExecutionManifest CreateManifest(
        string executionId,
        ExecutionStatus status,
        TimeSpan? duration,
        ExecutionEvent[]? events = null)
    {
        return new ExecutionManifest(
            executionId,
            null,
            null,
            status,
            DateTimeOffset.UtcNow,
            duration,
            new AgentRequest("test"),
            new AgentRuntimeOptions(),
            Array.Empty<string>(),
            1,
            0,
            0,
            events);
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmResponse("test"));
    }
}
