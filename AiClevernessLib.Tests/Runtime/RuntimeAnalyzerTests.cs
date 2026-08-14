using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace AiClevernessLib.Tests.Runtime;

// ── Test Helpers ──────────────────────────────────────────────────────────

public class RuntimeAnalyzerTests
{
    [Fact]
    public async Task Approval_NoToolsRequireApproval_ReportsInfo()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new GoodTool());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Approval)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info
                && f.Message.Contains("No tools require approval"));
    }

    // ── Approval Validation ───────────────────────────────────────────────

    [Fact]
    public async Task Approval_ToolRequiresApproval_NoService_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new ApprovalTool());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Approval)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Warning && f.ServiceName == "IApprovalService");
    }

    // ── DI Graph Validation ───────────────────────────────────────────────

    [Fact]
    public async Task EmptyContainer_ReportsRequiredErrors()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.IsHealthy.Should().BeFalse();
        result.GetFindings(RuntimeValidationCategory.DiGraph)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Error && f.ServiceName == "IAgentRuntime");
        result.GetFindings(RuntimeValidationCategory.DiGraph)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Error && f.ServiceName == "ILlmClient");
    }

    // ── Full Integration ──────────────────────────────────────────────────

    [Fact]
    public async Task FullSetup_AllCategories_Pass()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        services.AddInMemoryCheckpointStore();
        services.AddInMemoryExecutionJournal();
        services.AddInMemoryEventBus();
        services.AddMetricsCollector();
        services.AddDiagnosticCollector();
        services.AddStartupAnalyzer();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new GoodTool());
        var analyzer = sp.GetRequiredService<IStartupAnalyzer>();

        var result = await analyzer.AnalyzeAsync(sp);

        result.IsHealthy.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.Findings.Should().NotBeEmpty();
    }

    [Fact]
    public void GetErrors_ReturnsOnlyErrors()
    {
        var result = new StartupAnalysisResult
                         {
                             Findings = new[]
                                            {
                                                new StartupFinding(
                                                    "A",
                                                    StartupSeverity.Error,
                                                    "err"),
                                                new StartupFinding(
                                                    "B",
                                                    StartupSeverity.Warning,
                                                    "warn"),
                                                new StartupFinding(
                                                    "C",
                                                    StartupSeverity.Info,
                                                    "info"),
                                            }
                         };

        result.GetErrors().Should().HaveCount(1);
        result.GetErrors()[0].Message.Should().Be("err");
    }

    // ── StartupAnalysisResult Extensions ──────────────────────────────────

    [Fact]
    public void GetFindings_FiltersByCategory()
    {
        var result = new StartupAnalysisResult
                         {
                             Findings = new[]
                                            {
                                                new StartupFinding(
                                                    "A",
                                                    StartupSeverity.Error,
                                                    "err1")
                                                    {
                                                        Category = RuntimeValidationCategory.Tools
                                                    },
                                                new StartupFinding(
                                                    "B",
                                                    StartupSeverity.Warning,
                                                    "warn1")
                                                    {
                                                        Category = RuntimeValidationCategory.Tools
                                                    },
                                                new StartupFinding(
                                                    "C",
                                                    StartupSeverity.Error,
                                                    "err2")
                                                    {
                                                        Category = RuntimeValidationCategory
                                                            .Persistence
                                                    },
                                            }
                         };

        result.GetFindings(RuntimeValidationCategory.Tools).Should().HaveCount(2);
        result.GetFindings(RuntimeValidationCategory.Persistence).Should().HaveCount(1);
        result.GetFindings(RuntimeValidationCategory.Workflows).Should().BeEmpty();
    }

    // ── Observability Validation ──────────────────────────────────────────

    [Fact]
    public async Task Observability_NoEventPublisher_ReportsInfo()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Observability)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info && f.ServiceName == "IExecutionEventPublisher");
    }

    [Fact]
    public async Task Observability_WithEventBus_ReportsNoWarning()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        services.AddInMemoryEventBus();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Observability)
            .Should().NotContain(f =>
                f.ServiceName == "IExecutionEventPublisher"
                && f.Severity == StartupSeverity.Warning);
    }

    // ── Persistence Validation ────────────────────────────────────────────

    [Fact]
    public async Task Persistence_NoCheckpointStore_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Persistence)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Warning && f.ServiceName == "ICheckpointStore");
    }

    [Fact]
    public async Task Persistence_NoJournal_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Persistence)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Warning && f.ServiceName == "IExecutionJournal");
    }

    [Fact]
    public async Task Persistence_WithCheckpointStore_ReportsInfo()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        services.AddInMemoryCheckpointStore();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Persistence)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info && f.ServiceName == "ICheckpointStore");
    }

    // ── RuntimeValidationCategory ─────────────────────────────────────────

    [Fact]
    public void RuntimeValidationCategory_HasAllExpectedValues()
    {
        Enum.GetValues<RuntimeValidationCategory>().Should().HaveCount(6);
        Enum.IsDefined(RuntimeValidationCategory.DiGraph).Should().BeTrue();
        Enum.IsDefined(RuntimeValidationCategory.Tools).Should().BeTrue();
        Enum.IsDefined(RuntimeValidationCategory.Workflows).Should().BeTrue();
        Enum.IsDefined(RuntimeValidationCategory.Approval).Should().BeTrue();
        Enum.IsDefined(RuntimeValidationCategory.Persistence).Should().BeTrue();
        Enum.IsDefined(RuntimeValidationCategory.Observability).Should().BeTrue();
    }

    [Fact]
    public void StartupFinding_DefaultCategory_IsDiGraph()
    {
        var finding = new StartupFinding("svc", StartupSeverity.Info, "ok");
        finding.Category.Should().Be(RuntimeValidationCategory.DiGraph);
    }

    [Fact]
    public void ThrowOnErrors_NoErrors_DoesNotThrow()
    {
        var result = new StartupAnalysisResult
                         {
                             Findings = new[]
                                            {
                                                new StartupFinding(
                                                    "A",
                                                    StartupSeverity.Warning,
                                                    "warn"),
                                                new StartupFinding(
                                                    "B",
                                                    StartupSeverity.Info,
                                                    "info"),
                                            }
                         };

        var act = () => result.ThrowOnErrors();
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowOnErrors_WithErrors_ThrowsInvalidOperationException()
    {
        var result = new StartupAnalysisResult
                         {
                             Findings = new[]
                                            {
                                                new StartupFinding(
                                                    "A",
                                                    StartupSeverity.Error,
                                                    "missing service")
                                                    {
                                                        Category = RuntimeValidationCategory.DiGraph
                                                    },
                                                new StartupFinding(
                                                    "B",
                                                    StartupSeverity.Error,
                                                    "tool collision")
                                                    {
                                                        Category = RuntimeValidationCategory.Tools
                                                    },
                                            }
                         };

        var act = () => result.ThrowOnErrors();
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("startup validation failed");
        ex.Message.Should().Contain("DiGraph");
        ex.Message.Should().Contain("Tools");
        ex.Message.Should().Contain("missing service");
        ex.Message.Should().Contain("tool collision");
    }

    [Fact]
    public async Task Tools_DuplicateName_RegistryOverwrites_NoCollisionDetected()
    {
        // The default ToolRegistry uses a dictionary keyed by name,
        // so registering two tools with the same name overwrites the first.
        // The analyzer sees only 1 tool (the last registered).
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new DuplicateToolA());
        registry.Register(new DuplicateToolB());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        // Only 1 tool visible (last wins), no collision error
        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info && f.Message.Contains("1 tool(s)"));
        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().NotContain(f =>
                f.Severity == StartupSeverity.Error && f.Message.Contains("collision"));
    }

    [Fact]
    public async Task Tools_Empty_ReportsInfo()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info && f.Message.Contains("No tools registered"));
    }

    // ── Tool Validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Tools_GoodTool_ReportsInfoCount()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new GoodTool());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Info && f.Message.Contains("1 tool(s)"));
    }

    [Fact]
    public async Task Tools_NoDescription_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new NoDescriptionTool());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Warning && f.Message.Contains("no description"));
    }

    [Fact]
    public async Task Tools_NullDefinition_ReportsError()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        registry.Register(new NullDefinitionTool());
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.Tools)
            .Should().Contain(f =>
                f.Severity == StartupSeverity.Error && f.Message.Contains("null Definition"));
    }

    [Fact]
    public async Task ValidateAsync_Healthy_ReturnsServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        services.AddStartupAnalyzer();
        var sp = services.BuildServiceProvider();

        var result = await sp.ValidateAiClevernessAsync();

        result.Should().BeSameAs(sp);
    }

    // ── ValidateAiClevernessAsync Extension ───────────────────────────────

    [Fact]
    public async Task ValidateAsync_NoAnalyzer_ThrowsInvalidOperation()
    {
        var sp = new ServiceCollection().BuildServiceProvider();

        var act = async () => await sp.ValidateAiClevernessAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IStartupAnalyzer*");
    }

    [Fact]
    public async Task ValidateAsync_WithErrors_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        services.AddStartupAnalyzer();
        // Missing runtime and LLM client → errors
        var sp = services.BuildServiceProvider();

        var act = async () => await sp.ValidateAiClevernessAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*startup validation failed*");
    }

    [Fact]
    public async Task WithCoreServices_NoDiGraphErrors()
    {
        var services = new ServiceCollection();
        services.AddAiClevernessRuntime();
        services.AddAiClevernessLlmClient<FakeLlmClient>();
        var sp = services.BuildServiceProvider();
        var analyzer = new DefaultStartupAnalyzer();

        var result = await analyzer.AnalyzeAsync(sp);

        result.GetFindings(RuntimeValidationCategory.DiGraph)
            .Should().NotContain(f => f.Severity == StartupSeverity.Error);
    }

    [Fact]
    public void WorkflowValidation_AgentNodeWithoutRequest_ReportsWarning()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = null
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Warning && f.Message.Contains("no Request"));
    }

    [Fact]
    public void WorkflowValidation_ConditionWithoutExpression_ReportsWarning()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Condition,
                                                       Condition = null
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Warning && f.Message.Contains("no Condition"));
    }

    [Fact]
    public void WorkflowValidation_CycleDetected_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Agent,
                                                       DependsOn = new[] { "node2" }
                                                   },
                                               new WorkflowNode
                                                   {
                                                       Id = "node2",
                                                       Name = "B",
                                                       Type = WorkflowNodeType.Agent,
                                                       DependsOn = new[] { "node1" }
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("cycle"));
    }

    [Fact]
    public void WorkflowValidation_DuplicateNodeIds_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Agent
                                                   },
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "B",
                                                       Type = WorkflowNodeType.Agent
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("duplicate node ID"));
    }

    [Fact]
    public void WorkflowValidation_EmptyId_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Agent
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("empty or null Id"));
    }

    [Fact]
    public void WorkflowValidation_EmptyNodes_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = Array.Empty<WorkflowNode>()
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("no nodes"));
    }

    [Fact]
    public void WorkflowValidation_InvalidChildRef_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Parallel,
                                                       Children = new[] { "ghost-child" }
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("non-existent child"));
    }

    [Fact]
    public void WorkflowValidation_InvalidDependency_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "A",
                                                       Type = WorkflowNodeType.Agent,
                                                       DependsOn = new[] { "ghost" }
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("non-existent node 'ghost'"));
    }

    [Fact]
    public void WorkflowValidation_MissingEntryNode_ReportsError()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test",
                               EntryNodeId = "nonexistent",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "Step 1",
                                                       Type = WorkflowNodeType.Agent
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().Contain(f =>
            f.Severity == StartupSeverity.Error && f.Message.Contains("entry node"));
    }

    // ── Workflow Validation ───────────────────────────────────────────────

    [Fact]
    public void WorkflowValidation_ValidWorkflow_NoErrors()
    {
        var workflow = new WorkflowDefinition
                           {
                               Id = "wf1",
                               Name = "Test Workflow",
                               EntryNodeId = "node1",
                               Nodes = new[]
                                           {
                                               new WorkflowNode
                                                   {
                                                       Id = "node1",
                                                       Name = "Step 1",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = new AgentRequest("test")
                                                   },
                                               new WorkflowNode
                                                   {
                                                       Id = "node2",
                                                       Name = "Step 2",
                                                       Type = WorkflowNodeType.Agent,
                                                       Request = new AgentRequest("test"),
                                                       DependsOn = new[] { "node1" }
                                                   }
                                           }
                           };

        var findings = DefaultStartupAnalyzer.ValidateWorkflowDefinition(workflow);

        findings.Should().NotContain(f => f.Severity == StartupSeverity.Error);
    }

    private class ApprovalTool : ITool
    {
        public ToolDefinition Definition { get; } = new(
            "dangerous-tool",
            "Requires approval.",
            RequiresApproval: true);

        public string Description => "Requires approval.";

        public string Name => "dangerous-tool";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }

    private class DuplicateToolA : ITool
    {
        public ToolDefinition Definition { get; } = new("dup", "First dup.");

        public string Description => "First dup.";

        public string Name => "dup";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }

    private class DuplicateToolB : ITool
    {
        public ToolDefinition Definition { get; } = new("dup", "Second dup.");

        public string Description => "Second dup.";

        public string Name => "dup";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }

    private class FakeLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken ct = default) =>
            Task.FromResult(new LlmResponse("ok"));
    }

    private class GoodTool : ITool
    {
        public ToolDefinition Definition { get; } = new("good-tool", "A well-defined tool.");

        public string Description => "A well-defined tool.";

        public string Name => "good-tool";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }

    private class NoDescriptionTool : ITool
    {
        public ToolDefinition Definition { get; } = new("no-desc-tool", "");

        public string Description => "";

        public string Name => "no-desc-tool";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }

    private class NullDefinitionTool : ITool
    {
        public ToolDefinition Definition => null!;

        public string Description => "Has null definition.";

        public string Name => "null-def-tool";

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, "ok"));
    }
}
