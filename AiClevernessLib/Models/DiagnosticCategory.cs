namespace AiCleverness.Models;

/// <summary>
/// Category of a diagnostic entry indicating which subsystem produced it.
/// </summary>
public enum DiagnosticCategory
{
    ModelSelection,
    ToolSelection,
    Strategy,
    Planning,
    Policy,
    QualityGate,
    Resource,
    Runtime
}
