using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Models;

/// <summary>
/// Serializable graph representation of an execution's flow.
/// Suitable for rendering as DOT, Mermaid, or any graph visualization tool.
/// </summary>
public sealed record ExecutionGraph
{
    /// <summary>Total execution duration, if completed.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Directed edges connecting nodes.</summary>
    public IReadOnlyList<ExecutionGraphEdge> Edges { get; init; } =
        Array.Empty<ExecutionGraphEdge>();

    /// <summary>Execution identifier this graph represents.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>Final execution status.</summary>
    public ExecutionStatus FinalStatus { get; init; }

    /// <summary>UTC timestamp when the graph was generated.</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Graph nodes representing execution steps.</summary>
    public IReadOnlyList<ExecutionGraphNode> Nodes { get; init; } =
        Array.Empty<ExecutionGraphNode>();

    /// <summary>
    /// Creates an execution graph from a list of execution events.
    /// </summary>
    public static ExecutionGraph FromEvents(
        string executionId,
        ExecutionStatus finalStatus,
        TimeSpan? duration,
        IReadOnlyList<ExecutionEvent> events)
    {
        var nodes = new List<ExecutionGraphNode>();
        var edges = new List<ExecutionGraphEdge>();
        string? previousNodeId = null;
        int stepIndex = 0;

        // Add start node
        var startId = "start";
        nodes.Add(new ExecutionGraphNode(startId, "Start", ExecutionGraphNodeType.Start));
        previousNodeId = startId;

        foreach (var evt in events)
        {
            stepIndex++;
            var nodeId = $"step{stepIndex}";

            switch (evt)
            {
                case ExecutionStartedEvent:
                    // Already handled by start node
                    continue;

                case LlmCalledEvent:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"LLM Call #{stepIndex}",
                            ExecutionGraphNodeType.LlmCall,
                            evt.Timestamp));
                    break;

                case LlmRespondedEvent lre:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"LLM Response ({lre.Duration.TotalMilliseconds:F0}ms)",
                            ExecutionGraphNodeType.LlmCall,
                            evt.Timestamp));
                    break;

                case ToolInvokedEvent tie:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Tool: {tie.ToolName}",
                            ExecutionGraphNodeType.ToolCall,
                            evt.Timestamp));
                    break;

                case ToolCompletedEvent tce:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Tool: {tce.ToolName} ({tce.Duration.TotalMilliseconds:F0}ms)",
                            ExecutionGraphNodeType.ToolCall,
                            evt.Timestamp));
                    break;

                case QualityGateRejectedEvent qge:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Gate: {qge.GateName} (retry #{qge.RetryCount})",
                            ExecutionGraphNodeType.QualityGate,
                            evt.Timestamp));
                    break;

                case PolicyBlockedEvent pbe:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Policy: {pbe.PolicyName}",
                            ExecutionGraphNodeType.Policy,
                            evt.Timestamp));
                    break;

                case DecisionNodeVisitedEvent dnve:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Decision node: {dnve.NodeId} ({dnve.NodeType})",
                            ExecutionGraphNodeType.DecisionNode,
                            evt.Timestamp));
                    break;

                case DecisionActionCompletedEvent dace:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Decision action: {dace.ActionKey} ({dace.Status})",
                            ExecutionGraphNodeType.DecisionNode,
                            evt.Timestamp));
                    break;

                case DecisionClassificationCompletedEvent dce:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Decision classification: {dce.Answer}",
                            ExecutionGraphNodeType.DecisionNode,
                            evt.Timestamp));
                    break;

                case ExecutionCompletedEvent ece:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            $"Completed ({ece.Duration.TotalMilliseconds:F0}ms)",
                            ExecutionGraphNodeType.End,
                            evt.Timestamp));
                    break;

                default:
                    nodes.Add(
                        new ExecutionGraphNode(
                            nodeId,
                            evt.EventType,
                            ExecutionGraphNodeType.LlmCall,
                            evt.Timestamp));
                    break;
            }

            if (previousNodeId is not null && nodeId != startId)
            {
                edges.Add(new ExecutionGraphEdge(previousNodeId, nodeId));
            }

            previousNodeId = nodeId;
        }

        // Ensure we have an end node if no completion event was in the list
        if (nodes.Count > 0 && nodes[^1].Type != ExecutionGraphNodeType.End)
        {
            var endId = "end";
            nodes.Add(
                new ExecutionGraphNode(endId, finalStatus.ToString(), ExecutionGraphNodeType.End));
            edges.Add(new ExecutionGraphEdge(previousNodeId ?? startId, endId));
        }

        return new ExecutionGraph
                   {
                       ExecutionId = executionId,
                       FinalStatus = finalStatus,
                       Duration = duration,
                       Nodes = nodes.AsReadOnly(),
                       Edges = edges.AsReadOnly()
                   };
    }

    /// <summary>
    /// Exports the graph as a Mermaid-compatible flowchart string.
    /// </summary>
    public string ToMermaid()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("graph TB");

        foreach (var node in Nodes)
        {
            var shape = node.Type switch
                {
                    ExecutionGraphNodeType.Start => $"    {node.Id}[\"{node.Label}\"]",
                    ExecutionGraphNodeType.LlmCall => $"    {node.Id}{{\"{node.Label}\"}}",
                    ExecutionGraphNodeType.ToolCall => $"    {node.Id}[\"{node.Label}\"]",
                    ExecutionGraphNodeType.QualityGate => $"    {node.Id}{{\"{node.Label}\"}}",
                    ExecutionGraphNodeType.Policy => $"    {node.Id}{{\"{node.Label}\"}}",
                    ExecutionGraphNodeType.DecisionNode => $"    {node.Id}{{\"{node.Label}\"}}",
                    ExecutionGraphNodeType.End => $"    {node.Id}[\"{node.Label}\"]",
                    _ => $"    {node.Id}[\"{node.Label}\"]"
                };
            sb.AppendLine(shape);
        }

        foreach (var edge in Edges)
        {
            var label = string.IsNullOrEmpty(edge.Label) ? "" : $"|{edge.Label}|";
            sb.AppendLine($"    {edge.From} -->{label} {edge.To}");
        }

        return sb.ToString();
    }
}
