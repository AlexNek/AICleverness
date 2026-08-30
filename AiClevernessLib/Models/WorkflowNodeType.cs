namespace AiCleverness.Models;

/// <summary>Type of workflow node.</summary>
public enum WorkflowNodeType
{
    Agent,
    Condition,
    Parallel,
    Transform,
    Router
}
