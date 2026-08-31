namespace AiCleverness.Models;

/// <summary>Type of execution graph node.</summary>
public enum ExecutionGraphNodeType
{
    Start,
    LlmCall,
    ToolCall,
    QualityGate,
    Policy,
    DecisionNode,
    End
}
