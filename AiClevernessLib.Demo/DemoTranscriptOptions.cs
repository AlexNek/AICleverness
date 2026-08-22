using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Demo-only command-line configuration for exercising Markdown transcripts.
/// </summary>
internal sealed class DemoTranscriptOptions
{
    private DemoTranscriptOptions(bool enabled, bool debug, string directory)
    {
        Enabled = enabled;
        Debug = debug;
        Directory = directory;
    }

    public bool Debug { get; }

    public string Directory { get; }

    public bool Enabled { get; }

    public static DemoTranscriptOptions Parse(IReadOnlyList<string> args)
    {
        var debug = args.Any(IsDebugSwitch);
        var enabled = debug || args.Any(IsTranscriptSwitch);
        var directory = Path.Combine(AppContext.BaseDirectory, "transcripts");
        return new DemoTranscriptOptions(enabled, debug, directory);
    }

    public AgentRequest Apply(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enabled)
            return request;

        var parameters = new Dictionary<string, object>(request.Parameters)
                         {
                             [AgentPropertyKeys.MarkdownTranscriptDirectory] = Directory
                         };
        if (Debug)
            parameters[AgentPropertyKeys.MarkdownTranscriptDebug] = true;

        return request with { Parameters = parameters };
    }

    private static bool IsDebugSwitch(string value) =>
        string.Equals(value, "/d", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "--transcript-debug", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "--debug-transcript", StringComparison.OrdinalIgnoreCase);

    private static bool IsTranscriptSwitch(string value) =>
        string.Equals(value, "--transcript", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "/t", StringComparison.OrdinalIgnoreCase)
        || IsDebugSwitch(value);
}
