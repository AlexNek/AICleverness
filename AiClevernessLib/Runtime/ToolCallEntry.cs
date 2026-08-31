using System.Text;

namespace AiCleverness.Runtime;

/// <summary>Stores the accumulated fragments for one streamed tool call.</summary>
internal sealed class ToolCallEntry
{
    public StringBuilder Arguments { get; } = new();

    public string? Id { get; set; }

    public string? Name { get; set; }
}
