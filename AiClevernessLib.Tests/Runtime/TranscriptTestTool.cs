using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Tests.Runtime;

public sealed class TranscriptTestTool : ITool
{
    public ToolDefinition Definition => new(
        Name,
        Description,
        "{\"type\":\"object\",\"properties\":{\"message\":{\"type\":\"string\"}}}");

    public string Description => "Captures a test message.";

    public string Name => "capture";

    public Task<ToolResult> InvokeAsync(
        ToolInvocation invocation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ToolResult(true, "tool invoked"));
}
