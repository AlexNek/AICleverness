using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Default <see cref="IStartupAnalyzer"/> that validates required AiCleverness services
/// are registered in the DI container, plus tools, workflows, approval config, and persistence.
/// </summary>
public sealed class DefaultStartupAnalyzer : IStartupAnalyzer
{
    private readonly ILogger<DefaultStartupAnalyzer>? _logger;

    /// <summary>
    /// Creates a new instance of the analyzer.
    /// </summary>
    public DefaultStartupAnalyzer()
    {
    }

    /// <summary>
    /// Creates a new instance of the analyzer with logging.
    /// </summary>
    public DefaultStartupAnalyzer(ILogger<DefaultStartupAnalyzer>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<StartupAnalysisResult> AnalyzeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<StartupFinding>();

        ValidateDiGraph(serviceProvider, findings);
        ValidateTools(serviceProvider, findings);
        ValidateModelCatalog(serviceProvider, findings);
        ValidateWorkflows(serviceProvider, findings);
        ValidateApproval(serviceProvider, findings);
        ValidatePersistence(serviceProvider, findings);
        ValidateObservability(serviceProvider, findings);
        ValidateDecisionTreeServices(serviceProvider, findings);

        var result = new StartupAnalysisResult { Findings = findings.AsReadOnly() };

        if (!result.IsHealthy)
        {
            _logger?.LogWarning(
                "AiCleverness startup analysis found {ErrorCount} error(s) and {WarningCount} warning(s)",
                result.ErrorCount,
                result.WarningCount);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Validates a specific workflow definition. Can be called independently to validate
    /// workflow graphs before execution.
    /// </summary>
    public static IReadOnlyList<StartupFinding> ValidateWorkflowDefinition(
        WorkflowDefinition workflow)
    {
        var findings = new List<StartupFinding>();

        if (string.IsNullOrWhiteSpace(workflow.Id))
        {
            findings.Add(
                new StartupFinding(
                    "Workflow:empty-id",
                    StartupSeverity.Error,
                    "Workflow has an empty or null Id.",
                    "Provide a unique Id for each workflow.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
        }

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            findings.Add(
                new StartupFinding(
                    $"Workflow:{workflow.Id}",
                    StartupSeverity.Warning,
                    $"Workflow '{workflow.Id}' has no Name.",
                    "Provide a display name for the workflow.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
        }

        if (workflow.Nodes.Count == 0)
        {
            findings.Add(
                new StartupFinding(
                    $"Workflow:{workflow.Id}",
                    StartupSeverity.Error,
                    $"Workflow '{workflow.Id}' has no nodes.",
                    "Add at least one node to the workflow.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
            return findings.AsReadOnly();
        }

        // Check entry node exists
        var nodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        if (!nodeIds.Contains(workflow.EntryNodeId))
        {
            findings.Add(
                new StartupFinding(
                    $"Workflow:{workflow.Id}:EntryNode",
                    StartupSeverity.Error,
                    $"Workflow '{workflow.Id}' entry node '{workflow.EntryNodeId}' does not exist.",
                    "Ensure EntryNodeId references a valid node in the workflow.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
        }

        // Check for duplicate node IDs
        var duplicateIds = workflow.Nodes
            .GroupBy(n => n.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicateIds)
        {
            findings.Add(
                new StartupFinding(
                    $"Workflow:{workflow.Id}:Node:{dup}",
                    StartupSeverity.Error,
                    $"Workflow '{workflow.Id}' has duplicate node ID '{dup}'.",
                    "Ensure all node IDs are unique within a workflow.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
        }

        // Check dependency references
        foreach (var node in workflow.Nodes)
        {
            foreach (var dep in node.DependsOn)
            {
                if (!nodeIds.Contains(dep))
                {
                    findings.Add(
                        new StartupFinding(
                            $"Workflow:{workflow.Id}:Node:{node.Id}:DependsOn",
                            StartupSeverity.Error,
                            $"Node '{node.Id}' depends on non-existent node '{dep}'.",
                            "Ensure all DependsOn references point to valid node IDs.")
                            {
                                Category = RuntimeValidationCategory.Workflows
                            });
                }
            }

            // Check children references
            foreach (var child in node.Children)
            {
                if (!nodeIds.Contains(child))
                {
                    findings.Add(
                        new StartupFinding(
                            $"Workflow:{workflow.Id}:Node:{node.Id}:Children",
                            StartupSeverity.Error,
                            $"Node '{node.Id}' references non-existent child '{child}'.",
                            "Ensure all Children references point to valid node IDs.")
                            {
                                Category = RuntimeValidationCategory.Workflows
                            });
                }
            }

            // Agent nodes should have a request
            if (node.Type == WorkflowNodeType.Agent && node.Request is null)
            {
                findings.Add(
                    new StartupFinding(
                        $"Workflow:{workflow.Id}:Node:{node.Id}",
                        StartupSeverity.Warning,
                        $"Agent node '{node.Id}' has no Request configured.",
                        "Provide an AgentRequest for agent-type nodes.")
                        {
                            Category = RuntimeValidationCategory.Workflows
                        });
            }

            // Condition nodes should have a condition
            if (node.Type == WorkflowNodeType.Condition
                && string.IsNullOrWhiteSpace(node.Condition))
            {
                findings.Add(
                    new StartupFinding(
                        $"Workflow:{workflow.Id}:Node:{node.Id}",
                        StartupSeverity.Warning,
                        $"Condition node '{node.Id}' has no Condition expression.",
                        "Provide a Condition expression for condition-type nodes.")
                        {
                            Category = RuntimeValidationCategory.Workflows
                        });
            }
        }

        // Check for cycles using DFS (only if no duplicate IDs)
        if (duplicateIds.Count == 0 && HasCycle(workflow.Nodes))
        {
            findings.Add(
                new StartupFinding(
                    $"Workflow:{workflow.Id}:Cycle",
                    StartupSeverity.Error,
                    $"Workflow '{workflow.Id}' contains a dependency cycle.",
                    "Remove circular dependencies between nodes.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
        }

        return findings.AsReadOnly();
    }

    private static void CheckRecommended<T>(
        IServiceProvider sp,
        List<StartupFinding> findings,
        RuntimeValidationCategory category,
        string suggestion)
    {
        var service = sp.GetService(typeof(T));
        if (service is null)
        {
            findings.Add(
                new StartupFinding(
                    typeof(T).Name,
                    StartupSeverity.Warning,
                    $"Recommended service {typeof(T).Name} is not registered.",
                    suggestion) { Category = category });
        }
        else
        {
            findings.Add(
                new StartupFinding(
                    typeof(T).Name,
                    StartupSeverity.Info,
                    $"{typeof(T).Name} is registered.") { Category = category });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CheckRequired<T>(
        IServiceProvider sp,
        List<StartupFinding> findings,
        RuntimeValidationCategory category,
        string suggestion)
    {
        var service = sp.GetService(typeof(T));
        if (service is null)
        {
            findings.Add(
                new StartupFinding(
                    typeof(T).Name,
                    StartupSeverity.Error,
                    $"Required service {typeof(T).Name} is not registered.",
                    suggestion) { Category = category });
        }
        else
        {
            findings.Add(
                new StartupFinding(
                    typeof(T).Name,
                    StartupSeverity.Info,
                    $"{typeof(T).Name} is registered.") { Category = category });
        }
    }

    private static bool DfsHasCycle(
        string nodeId,
        Dictionary<string, WorkflowNode> nodeMap,
        HashSet<string> visited,
        HashSet<string> inStack)
    {
        if (inStack.Contains(nodeId)) return true;
        if (visited.Contains(nodeId)) return false;

        visited.Add(nodeId);
        inStack.Add(nodeId);

        if (nodeMap.TryGetValue(nodeId, out var node))
        {
            foreach (var dep in node.DependsOn)
            {
                if (nodeMap.ContainsKey(dep) && DfsHasCycle(dep, nodeMap, visited, inStack))
                    return true;
            }
        }

        inStack.Remove(nodeId);
        return false;
    }

    private static bool HasCycle(IReadOnlyList<WorkflowNode> nodes)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id, n => n);
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var node in nodes)
        {
            if (visited.Contains(node.Id)) continue;
            if (DfsHasCycle(node.Id, nodeMap, visited, inStack))
                return true;
        }

        return false;
    }

    // ── Approval ──────────────────────────────────────────────────────────────

    private static void ValidateApproval(IServiceProvider sp, List<StartupFinding> findings)
    {
        var approvalService = sp.GetService<IApprovalService>();
        var registry = sp.GetService<IToolRegistry>();

        if (registry is null) return; // Tool validation already covers this

        var toolsRequiringApproval = registry.GetAllTools()
            .Where(t => t.Definition?.RequiresApproval == true)
            .ToList();

        if (toolsRequiringApproval.Count == 0)
        {
            findings.Add(
                new StartupFinding(
                    "Approval",
                    StartupSeverity.Info,
                    "No tools require approval. AutoApprovalService will allow all tool calls.")
                    {
                        Category = RuntimeValidationCategory.Approval
                    });
            return;
        }

        findings.Add(
            new StartupFinding(
                "Approval",
                StartupSeverity.Info,
                $"{toolsRequiringApproval.Count} tool(s) require approval: {string.Join(", ", toolsRequiringApproval.Select(t => t.Name))}.")
                {
                    Category = RuntimeValidationCategory.Approval
                });

        if (approvalService is null)
        {
            findings.Add(
                new StartupFinding(
                    "IApprovalService",
                    StartupSeverity.Warning,
                    "Tools require approval but no IApprovalService is registered.",
                    "Register an IApprovalService implementation or remove RequiresApproval from tool definitions.")
                    {
                        Category = RuntimeValidationCategory.Approval
                    });
        }
    }

    // ── Decision trees ────────────────────────────────────────────────────────

    private static void ValidateDecisionTreeServices(
        IServiceProvider sp,
        List<StartupFinding> findings)
    {
        if (sp.GetService<DecisionTreeExecutionOptions>() is null)
            return;

        CheckRequired<DecisionTreeExecutor>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Call AddDecisionTreeExecution() to register decision-tree services.");
        CheckRequired<ILlmCompletionPipeline>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Register an ILlmCompletionPipeline or call AddDecisionTreeExecution().");
        CheckRequired<IDecisionTreeLoader>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Register an IDecisionTreeLoader or call AddDecisionTreeExecution().");
        CheckRequired<IDecisionLlmContextBuilder>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Register an IDecisionLlmContextBuilder or call AddDecisionTreeExecution().");
    }

    // ── DI Graph ──────────────────────────────────────────────────────────────

    private static void ValidateDiGraph(IServiceProvider sp, List<StartupFinding> findings)
    {
        // Required services
        CheckRequired<IAgentRuntime>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Call AddAiClevernessRuntime() to register the agent runtime.");

        CheckRequired<ILlmClient>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "Call AddAiClevernessLlmClient<T>() with your LLM implementation.");

        CheckRequired<IToolRegistry>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "AddAiClevernessRuntime() registers a default ToolRegistry.");

        CheckRequired<IToolExecutor>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "AddAiClevernessRuntime() registers a DefaultToolExecutor.");

        CheckRequired<IAgentMemory>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "AddAiClevernessRuntime() registers InMemoryAgentMemory.");

        CheckRequired<AgentRuntimeOptions>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "AddAiClevernessRuntime() registers default options.");

        // Recommended services
        CheckRecommended<IAgentPlanner>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "No planner registered. Call AddDefaultPlanner() or AddSequentialPlanner() for planning support.");

        CheckRecommended<IShutdownCoordinator>(
            sp,
            findings,
            RuntimeValidationCategory.DiGraph,
            "No shutdown coordinator. Call AddShutdownCoordinator() for graceful shutdown support.");
    }

    // ── Model Catalog ─────────────────────────────────────────────────────────

    private static void ValidateModelCatalog(IServiceProvider sp, List<StartupFinding> findings)
    {
        var catalog = sp.GetService<IModelCatalog>();
        var resolver = sp.GetService<ICapabilityResolver>();

        if (catalog is null)
        {
            findings.Add(
                new StartupFinding(
                    "IModelCatalog",
                    StartupSeverity.Info,
                    "No model catalog registered. Model resolution via IModelManager will not be available.",
                    "Call AddModelCatalog() to register a model catalog.")
                    {
                        Category = RuntimeValidationCategory.DiGraph
                    });
            return;
        }

        if (resolver is null)
            return;

        var profiles = resolver.GetProfiles();
        foreach (var profile in profiles.Where(p => p.IsAvailable))
        {
            var candidates = catalog.GetCandidatesAsync(profile).AsTask().GetAwaiter().GetResult();
            if (candidates.Count == 0)
            {
                findings.Add(
                    new StartupFinding(
                        $"ModelCatalog:{profile.Id}",
                        StartupSeverity.Error,
                        $"Profile '{profile.Id}' has no model mapping. Add an entry in the catalog configuration.")
                        {
                            Category = RuntimeValidationCategory.DiGraph
                        });
            }
        }
    }

    // ── Observability ─────────────────────────────────────────────────────────

    private static void ValidateObservability(IServiceProvider sp, List<StartupFinding> findings)
    {
        // Observers
        var observers = sp.GetService(typeof(IEnumerable<IAgentObserver>))
                            as IEnumerable<IAgentObserver>;
        if (observers is null || !observers.Any())
        {
            findings.Add(
                new StartupFinding(
                    "IAgentObserver",
                    StartupSeverity.Info,
                    "No observers registered. Execution lifecycle events will not be observed.",
                    "Call AddAgentObserver<T>() to register observers.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }
        else
        {
            findings.Add(
                new StartupFinding(
                    "IAgentObserver",
                    StartupSeverity.Info,
                    $"{observers.Count()} observer(s) registered.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }

        // Policies
        var policies = sp.GetService(typeof(IEnumerable<IAgentPolicy>))
                           as IEnumerable<IAgentPolicy>;
        if (policies is null || !policies.Any())
        {
            findings.Add(
                new StartupFinding(
                    "IAgentPolicy",
                    StartupSeverity.Info,
                    "No policies registered. Execution will proceed without policy checks.",
                    "Call AddAgentPolicy<T>() to add policy enforcement.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }

        // Event publisher
        var eventPublisher = sp.GetService<IExecutionEventPublisher>();
        if (eventPublisher is null)
        {
            findings.Add(
                new StartupFinding(
                    "IExecutionEventPublisher",
                    StartupSeverity.Info,
                    "No event publisher registered. Execution bus events will not be published.",
                    "Call AddInMemoryEventBus() to enable the event bus.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }

        // Metrics
        var metrics = sp.GetService<IMetricsCollector>();
        if (metrics is null)
        {
            findings.Add(
                new StartupFinding(
                    "IMetricsCollector",
                    StartupSeverity.Info,
                    "No metrics collector registered.",
                    "Call AddMetricsCollector() for execution observability.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }

        // Diagnostics
        var diagnostics = sp.GetService<IDiagnosticCollector>();
        if (diagnostics is null)
        {
            findings.Add(
                new StartupFinding(
                    "IDiagnosticCollector",
                    StartupSeverity.Info,
                    "No diagnostic collector registered.",
                    "Call AddDiagnosticCollector() for decision tracing.")
                    {
                        Category = RuntimeValidationCategory.Observability
                    });
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private static void ValidatePersistence(IServiceProvider sp, List<StartupFinding> findings)
    {
        var checkpointStore = sp.GetService<ICheckpointStore>();
        var journal = sp.GetService<IExecutionJournal>();

        if (checkpointStore is null)
        {
            findings.Add(
                new StartupFinding(
                    "ICheckpointStore",
                    StartupSeverity.Warning,
                    "No checkpoint store registered. Execution persistence and replay will not be available.",
                    "Call AddInMemoryCheckpointStore() or AddCheckpointStore<T>() for persistence.")
                    {
                        Category = RuntimeValidationCategory.Persistence
                    });
        }
        else
        {
            findings.Add(
                new StartupFinding(
                    "ICheckpointStore",
                    StartupSeverity.Info,
                    "Checkpoint store is registered.")
                    {
                        Category = RuntimeValidationCategory.Persistence
                    });
        }

        if (journal is null)
        {
            findings.Add(
                new StartupFinding(
                    "IExecutionJournal",
                    StartupSeverity.Warning,
                    "No execution journal registered. Event journaling and replay will not be available.",
                    "Call AddInMemoryExecutionJournal() or AddExecutionJournal<T>() for journaling.")
                    {
                        Category = RuntimeValidationCategory.Persistence
                    });
        }
        else
        {
            findings.Add(
                new StartupFinding(
                    "IExecutionJournal",
                    StartupSeverity.Info,
                    "Execution journal is registered.")
                    {
                        Category = RuntimeValidationCategory.Persistence
                    });
        }
    }

    // ── Tools ─────────────────────────────────────────────────────────────────

    private static void ValidateTools(IServiceProvider sp, List<StartupFinding> findings)
    {
        var registry = sp.GetService<IToolRegistry>();
        if (registry is null)
        {
            findings.Add(
                new StartupFinding(
                    "IToolRegistry",
                    StartupSeverity.Error,
                    "Cannot validate tools: IToolRegistry is not registered.",
                    "Call AddAiClevernessRuntime() to register the tool registry.")
                    {
                        Category = RuntimeValidationCategory.Tools
                    });
            return;
        }

        var tools = registry.GetAllTools();

        if (tools.Count == 0)
        {
            findings.Add(
                new StartupFinding(
                    "ITool",
                    StartupSeverity.Info,
                    "No tools registered. Agents will execute without tool support.",
                    "Call AddAgentTool<T>() to register tools.")
                    {
                        Category = RuntimeValidationCategory.Tools
                    });
            return;
        }

        findings.Add(
            new StartupFinding(
                "ITool",
                StartupSeverity.Info,
                $"{tools.Count} tool(s) registered.")
                {
                    Category = RuntimeValidationCategory.Tools
                });

        // Check for name collisions
        var nameGroups = tools.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var group in nameGroups.Where(g => g.Count() > 1))
        {
            findings.Add(
                new StartupFinding(
                    $"Tool:{group.Key}",
                    StartupSeverity.Error,
                    $"Tool name collision: '{group.Key}' is registered {group.Count()} times.",
                    "Ensure each tool has a unique Name property.")
                    {
                        Category = RuntimeValidationCategory.Tools
                    });
        }

        // Validate individual tool definitions
        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                findings.Add(
                    new StartupFinding(
                        "Tool:empty-name",
                        StartupSeverity.Error,
                        "A tool has an empty or null name.",
                        "Ensure all tools provide a non-empty Name.")
                        {
                            Category = RuntimeValidationCategory.Tools
                        });
            }

            if (string.IsNullOrWhiteSpace(tool.Description))
            {
                findings.Add(
                    new StartupFinding(
                        $"Tool:{tool.Name}",
                        StartupSeverity.Warning,
                        $"Tool '{tool.Name}' has no description. LLMs may not use it correctly.",
                        "Provide a meaningful Description on the tool.")
                        {
                            Category = RuntimeValidationCategory.Tools
                        });
            }

            if (tool.Definition is null)
            {
                findings.Add(
                    new StartupFinding(
                        $"Tool:{tool.Name}",
                        StartupSeverity.Error,
                        $"Tool '{tool.Name}' has a null Definition.",
                        "Ensure the tool returns a valid ToolDefinition.")
                        {
                            Category = RuntimeValidationCategory.Tools
                        });
            }
            else if (string.IsNullOrWhiteSpace(tool.Definition.Name))
            {
                findings.Add(
                    new StartupFinding(
                        $"Tool:{tool.Name}",
                        StartupSeverity.Warning,
                        $"Tool '{tool.Name}' has a ToolDefinition with an empty Name.",
                        "Ensure ToolDefinition.Name matches the tool's Name.")
                        {
                            Category = RuntimeValidationCategory.Tools
                        });
            }
        }
    }

    // ── Workflows ─────────────────────────────────────────────────────────────

    private static void ValidateWorkflows(IServiceProvider sp, List<StartupFinding> findings)
    {
        // Check for workflow executors
        var executors = sp.GetService(typeof(IEnumerable<IWorkflowExecutor>))
                            as IEnumerable<IWorkflowExecutor>;

        if (executors is null || !executors.Any())
        {
            findings.Add(
                new StartupFinding(
                    "IWorkflowExecutor",
                    StartupSeverity.Info,
                    "No workflow executors registered. Workflow execution will not be available.",
                    "Register a workflow executor if you plan to use workflows.")
                    {
                        Category = RuntimeValidationCategory.Workflows
                    });
            return;
        }

        findings.Add(
            new StartupFinding(
                "IWorkflowExecutor",
                StartupSeverity.Info,
                $"{executors.Count()} workflow executor(s) registered: {string.Join(", ", executors.Select(e => e.Name))}.")
                {
                    Category = RuntimeValidationCategory.Workflows
                });
    }
}
