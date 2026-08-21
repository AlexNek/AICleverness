using System.Text;

using AiCleverness.Models;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Builds individual transcript sections without retaining the whole document.
/// </summary>
internal sealed class MarkdownTranscriptBuilder
{
    public string Header(
        string goal,
        string executionId,
        DateTimeOffset startedAt,
        bool debug)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Agent task");
        builder.AppendLine();
        builder.AppendLine($"**Execution ID:** `{executionId}`  ");
        builder.AppendLine($"**Started:** `{startedAt:O}`  ");
        builder.AppendLine($"**Debug mode:** `{debug}`");
        builder.AppendLine();
        builder.AppendLine("## Request");
        builder.AppendLine();
        builder.Append(Fenced(goal));
        builder.AppendLine();
        return builder.ToString();
    }

    public string Turn(int turn, int attempt, string? model)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## Turn {turn}");
        builder.AppendLine();
        builder.AppendLine($"**Attempt:** `{attempt}`  ");
        builder.AppendLine($"**Model:** `{model ?? "unknown"}`");
        builder.AppendLine();
        return builder.ToString();
    }

    public string ModelContent(string content)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Model content");
        builder.AppendLine();
        builder.Append(Fenced(content));
        builder.AppendLine();
        return builder.ToString();
    }

    public string ToolDecision(string model, string toolName, string arguments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Model decision");
        builder.AppendLine();
        builder.AppendLine($"**Model:** `{model}`  ");
        builder.AppendLine($"**Tool:** `{toolName}`");
        builder.AppendLine();
        builder.AppendLine("**Arguments:**");
        builder.AppendLine();
        builder.Append(Fenced(arguments, "json"));
        builder.AppendLine();
        return builder.ToString();
    }

    public string ToolResult(
        string toolName,
        string status,
        string? output,
        string? error)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### Tool result: `{toolName}`");
        builder.AppendLine();
        builder.AppendLine($"**Status:** `{status}`");
        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.AppendLine();
            builder.AppendLine("**Error:**");
            builder.AppendLine();
            builder.Append(Fenced(error));
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            builder.AppendLine();
            builder.AppendLine("**Output:**");
            builder.AppendLine();
            builder.Append(Fenced(output));
        }

        builder.AppendLine();
        return builder.ToString();
    }

    public string Retry(string reason, int retryNumber)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### Quality retry {retryNumber}");
        builder.AppendLine();
        builder.Append(Fenced(reason));
        builder.AppendLine();
        return builder.ToString();
    }

    public string Status(string status, string? detail)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### Execution status: `{status}`");
        if (!string.IsNullOrWhiteSpace(detail))
        {
            builder.AppendLine();
            builder.Append(Fenced(detail));
        }

        builder.AppendLine();
        return builder.ToString();
    }

    public string Final(AgentResult result, string status)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Final response");
        builder.AppendLine();
        builder.AppendLine($"**Status:** `{status}`");
        builder.AppendLine();
        var content = result.Success ? result.Output : result.Reasoning;
        builder.Append(Fenced(content ?? "(no response)"));
        builder.AppendLine();
        return builder.ToString();
    }

    public static string Fenced(string content, string? language = null)
    {
        content ??= string.Empty;
        var fenceLength = 3;
        var currentRun = 0;
        foreach (var character in content)
        {
            if (character == '`')
            {
                currentRun++;
                fenceLength = Math.Max(fenceLength, currentRun + 1);
            }
            else
            {
                currentRun = 0;
            }
        }

        var fence = new string('`', fenceLength);
        var languageSuffix = string.IsNullOrWhiteSpace(language) ? string.Empty : language;
        return $"{fence}{languageSuffix}{Environment.NewLine}{content}{Environment.NewLine}{fence}{Environment.NewLine}";
    }
}
