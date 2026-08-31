using System.Text;

namespace AiCleverness.Runtime;

/// <summary>Accumulates chunks for a single streaming tool call.</summary>
internal sealed class ToolCallAccumulator
{
    public StringBuilder ArgumentsBuilder { get; } = new();
    public string? FunctionName { get; set; }
    public string ToolCallId { get; }

    public ToolCallAccumulator(string toolCallId)
    {
        ToolCallId = toolCallId;
    }

    public string GetArguments() => ArgumentsBuilder.ToString();
}
