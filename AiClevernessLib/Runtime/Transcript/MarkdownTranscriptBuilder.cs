using System.Collections;
using System.Globalization;
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

    public string DebugRequest(IReadOnlyDictionary<string, object> parameters)
    {
        var values = parameters
            .Where(pair => !string.Equals(
                pair.Key,
                AgentPropertyKeys.SystemPrompt,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}: {FormatDebugValue(pair.Value)}");
        var builder = new StringBuilder();
        builder.AppendLine("## Debug request parameters");
        builder.AppendLine();
        builder.Append(Fenced(string.Join(Environment.NewLine, values)));
        builder.AppendLine();
        return builder.ToString();
    }

    public string DebugRuntime(
        ExecutionMetadata metadata,
        ExecutionState state,
        ModelExecutionInfo? modelExecutionInfo,
        string systemPrompt,
        bool includeSystemPrompt,
        string? qualityFeedback,
        int maxTurns,
        float temperature,
        int completionTimeoutSeconds,
        int idleTimeoutSeconds,
        string? model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Debug runtime");
        builder.AppendLine();
        if (includeSystemPrompt)
            AppendFencedValue(builder, "System prompt", systemPrompt);

        AppendFencedValue(builder, "Quality feedback", qualityFeedback ?? "(none)");
        AppendFencedValue(builder, "Model", model ?? "unknown");
        AppendFencedValue(builder, "Trace ID", metadata.TraceId ?? "(none)");
        AppendFencedValue(builder, "Correlation ID", metadata.CorrelationId ?? "(none)");
        AppendFencedValue(
            builder,
            "Available tools",
            string.Join(", ", metadata.AvailableToolNames));
        if (modelExecutionInfo is not null)
        {
            AppendFencedValue(builder, "Selected profile", modelExecutionInfo.Profile.Id);
            AppendFencedValue(
                builder,
                "Selection reason",
                modelExecutionInfo.SelectionReason ?? "(none)");
            builder.AppendLine($"**Model resolution attempt:** `{modelExecutionInfo.Attempt}`  ");
            builder.AppendLine($"**Fallback model:** `{modelExecutionInfo.IsFallback}`  ");
            builder.AppendLine($"**Remaining fallbacks:** `{modelExecutionInfo.RemainingFallbacks}`");
        }

        builder.AppendLine($"**Execution status:** `{state.Status}`  ");
        builder.AppendLine($"**Turn count:** `{state.TurnCount}`  ");
        builder.AppendLine($"**Tool invocation count:** `{state.ToolInvocationCount}`  ");
        builder.AppendLine($"**Quality retry count:** `{state.QualityRetryCount}`  ");
        builder.AppendLine($"**Max turns:** `{maxTurns}`  ");
        builder.AppendLine($"**Temperature:** `{temperature.ToString(CultureInfo.InvariantCulture)}`  ");
        builder.AppendLine($"**Completion timeout seconds:** `{completionTimeoutSeconds}`  ");
        builder.AppendLine($"**Idle timeout seconds:** `{idleTimeoutSeconds}`");
        builder.AppendLine();
        return builder.ToString();
    }

    public string Turn(int turn, int qualityAttempt, int failoverAttempt, string? model)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## Turn {turn}");
        builder.AppendLine();
        builder.AppendLine($"**Quality attempt:** `{qualityAttempt}`  ");
        builder.AppendLine($"**Failover attempt:** `{failoverAttempt}`  ");
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

    public string ToolDecision(
        string model,
        string toolName,
        string? callId,
        string arguments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Model decision");
        builder.AppendLine();
        builder.AppendLine($"**Model:** `{model}`  ");
        builder.AppendLine($"**Tool:** `{toolName}`");
        if (!string.IsNullOrWhiteSpace(callId))
            AppendFencedValue(builder, "Call ID", callId);

        builder.AppendLine("**Arguments:**");
        builder.AppendLine();
        builder.Append(Fenced(arguments, "json"));
        builder.AppendLine();
        return builder.ToString();
    }

    public string ToolResult(
        string toolName,
        string? callId,
        string status,
        string? output,
        string? error)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### Tool result: `{toolName}`");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(callId))
            AppendFencedValue(builder, "Call ID", callId);

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

    public string FinalFailure(string status, string detail)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Final response");
        builder.AppendLine();
        builder.AppendLine($"**Status:** `{status}`");
        builder.AppendLine();
        builder.Append(Fenced(detail));
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

    private static void AppendFencedValue(StringBuilder builder, string label, string value)
    {
        builder.AppendLine($"**{label}:**");
        builder.Append(Fenced(value));
    }

    private static string FormatDebugValue(object? value)
    {
        if (value is null)
            return "(null)";

        if (value is string text)
            return text;

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return FormatDebugEntries(readOnlyDictionary);

        if (value is IDictionary<string, object> dictionary)
            return FormatDebugEntries(dictionary);

        if (value is IEnumerable enumerable)
            return $"[{string.Join(", ", enumerable.Cast<object?>().Select(FormatDebugValue))}]";

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    private static string FormatDebugEntries(
        IEnumerable<KeyValuePair<string, object>> entries)
    {
        return $"{{{string.Join(", ", entries
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}: {FormatDebugValue(pair.Value)}"))}}}";
    }
}
