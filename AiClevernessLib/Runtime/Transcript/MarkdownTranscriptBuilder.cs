using System.Collections;
using System.Globalization;
using System.Text;

using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Builds individual transcript sections without retaining the whole document.
/// </summary>
public sealed class MarkdownTranscriptBuilder : ITranscriptBuilder
{
    private const int MinimumFenceLength = 3;
    private const int MaxBoundedCollectionItems = 100;
    private const int MaxBoundedCollectionDepth = 10;
    private const string JsonLanguageTag = "json";
    private const string BoundedCollectionItemsMarker = "[items omitted]";
    private const string BoundedCollectionDepthMarker = "[maximum nesting depth reached]";
    private const string BoundedCollectionCycleMarker = "[reference cycle detected]";

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

    public string DecisionOverview(
        string treeId,
        int version,
        string startNodeId,
        string task)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Decision task");
        builder.AppendLine();
        builder.AppendLine($"**Tree:** `{treeId}`  ");
        builder.AppendLine($"**Version:** `{version}`  ");
        builder.AppendLine($"**Start node:** `{startNodeId}`");
        AppendFencedValue(builder, "Task", task);
        builder.AppendLine("---");
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
        builder.Append(Fenced(arguments, JsonLanguageTag));
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

    public string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData)
    {
        var builder = new StringBuilder();
        var displayName = string.IsNullOrWhiteSpace(nodeName) ? actionKey : nodeName;
        builder.AppendLine($"### Decision action: `{displayName}`");
        builder.AppendLine();
        builder.AppendLine($"**Node:** `{nodeId}`  ");
        builder.AppendLine($"**Status:** `{status}`");
        if (!string.IsNullOrWhiteSpace(outcomeSummary))
            AppendFencedValue(builder, "Outcome", outcomeSummary);
        if (!string.IsNullOrWhiteSpace(error))
            AppendFencedValue(builder, "Error", error);
        if (!string.IsNullOrWhiteSpace(producedData))
            AppendFencedValue(builder, "Produced evidence/data", producedData);

        builder.AppendLine();
        return builder.ToString();
    }

    public string DecisionClassification(
        string nodeId,
        string answer,
        string? observation,
        string? confidence,
        int attempt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Parsed classification");
        builder.AppendLine();
        builder.AppendLine($"**Node:** `{nodeId}`  ");
        builder.AppendLine($"**Answer:** `{answer}`  ");
        builder.AppendLine($"**Attempt:** `{attempt}`");
        if (!string.IsNullOrWhiteSpace(observation))
            AppendFencedValue(builder, "Observation", observation);
        if (!string.IsNullOrWhiteSpace(confidence))
            AppendFencedValue(builder, "Confidence", confidence);

        builder.AppendLine();
        return builder.ToString();
    }

    public string DecisionLlmAttempt(
        string nodeId,
        int attempt,
        IReadOnlyList<LlmMessage> messages,
        string? response,
        string? finishReason,
        LlmTokenUsage? usage)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### Decision LLM attempt {attempt}");
        builder.AppendLine();
        builder.AppendLine($"**Node:** `{nodeId}`");
        builder.AppendLine();
        builder.AppendLine("**Input messages:**");
        builder.AppendLine();
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            builder.AppendLine($"#### Message {index + 1}: `{message.Role}`");
            builder.AppendLine();
            builder.Append(Fenced(message.Content ?? "(empty)"));
        }

        builder.AppendLine("**Raw LLM output:**");
        var displayResponse = string.IsNullOrWhiteSpace(response)
            ? "(empty)"
            : EnumAnswerParser.StripCodeFences(response);
        builder.Append(Fenced(displayResponse, JsonLanguageTag));
        if (!string.IsNullOrWhiteSpace(finishReason))
            builder.AppendLine($"**Finish reason:** `{finishReason}`");
        if (usage is not null)
        {
            builder.AppendLine($"**Prompt tokens:** `{usage.PromptTokens}`  ");
            builder.AppendLine($"**Completion tokens:** `{usage.CompletionTokens}`  ");
            builder.AppendLine($"**Total tokens:** `{usage.TotalTokens}`");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    public string DecisionResult(
        DecisionTreeOutcome outcome,
        bool succeeded,
        string? verdict,
        string? error,
        ResourceUsage usage,
        IReadOnlyList<string> path,
        int omittedSectionCount = 0,
        IReadOnlyList<KeyValuePair<string, string>>? stateProperties = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Decision result");
        builder.AppendLine();
        builder.AppendLine($"**Outcome:** `{outcome}`  ");
        builder.AppendLine($"**Succeeded:** `{succeeded}`");
        if (!string.IsNullOrWhiteSpace(verdict))
            AppendFencedValue(builder, "Verdict", verdict);
        if (!string.IsNullOrWhiteSpace(error))
            AppendFencedValue(builder, "Error", error);

        if (stateProperties is { Count: > 0 })
        {
            builder.AppendLine("### State properties");
            builder.AppendLine();
            foreach (var property in stateProperties)
            {
                var label = EscapeStatePropertyLabel(property.Key);
                if (IsInlineStateProperty(property.Value))
                    builder.AppendLine($"**{label}:** `{property.Value}`");
                else
                    AppendFencedValue(builder, label, property.Value);
            }

            builder.AppendLine();
        }

        builder.AppendLine("### Selected path");
        builder.AppendLine();
        for (var index = 0; index < path.Count; index++)
            builder.AppendLine($"{index + 1}. {path[index]}");
        if (omittedSectionCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"**Decision transcript sections omitted:** `{omittedSectionCount}`");
        }
        builder.AppendLine();

        builder.AppendLine("### Decision budget");
        builder.AppendLine();
        builder.AppendLine($"**Node visits:** `{usage.NodeVisits}`  ");
        builder.AppendLine($"**LLM calls:** `{usage.LlmCalls}`  ");
        builder.AppendLine($"**Input tokens:** `{usage.InputTokens}`  ");
        builder.AppendLine($"**Output tokens:** `{usage.OutputTokens}`  ");
        builder.AppendLine($"**Total tokens:** `{usage.TotalTokens}`  ");
        builder.AppendLine($"**Duration:** `{usage.Duration.TotalMilliseconds:F0}ms`");
        builder.AppendLine();
        return builder.ToString();
    }

    private static string EscapeStatePropertyLabel(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static bool IsInlineStateProperty(string value)
        => !value.Contains('`') && !value.Contains('\r') && !value.Contains('\n');

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
        var fenceLength = MinimumFenceLength;
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

    internal static string FormatDebugValue(object? value)
    {
        if (value is null)
            return "(null)";

        if (value is string text)
            return text;

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return FormatDebugEntries(readOnlyDictionary);

        if (value is IDictionary<string, object> dictionary)
            return FormatDebugEntries(dictionary);

        if (value is System.Collections.IDictionary nonGenericDictionary
            && nonGenericDictionary.Keys.Cast<object?>().All(key => key is string))
        {
            return FormatDebugEntries(nonGenericDictionary.Keys
                .Cast<string>()
                .Select(key => new KeyValuePair<string, object>(
                    key,
                    nonGenericDictionary[key]!)));
        }

        if (value is IEnumerable enumerable)
        {
            var items = enumerable
                .Cast<object?>()
                .Select(FormatDebugValue)
                .OrderBy(item => item, StringComparer.Ordinal);
            return $"[{string.Join(", ", items)}]";
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    internal static string FormatBoundedDebugValue(object? value)
    {
        return FormatBoundedDebugValue(
            value,
            depth: 0,
            activeReferences: new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static string FormatBoundedDebugValue(
        object? value,
        int depth,
        HashSet<object> activeReferences)
    {
        if (value is null)
            return "(null)";

        if (value is string text)
            return text;

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
        {
            return FormatBoundedDebugEntries(
                readOnlyDictionary,
                readOnlyDictionary,
                depth,
                activeReferences);
        }

        if (value is IDictionary<string, object> dictionary)
        {
            return FormatBoundedDebugEntries(
                dictionary,
                dictionary,
                depth,
                activeReferences);
        }

        if (value is System.Collections.IDictionary nonGenericDictionary)
            return FormatBoundedNonGenericDictionary(
                nonGenericDictionary,
                depth,
                activeReferences);

        if (value is IEnumerable enumerable)
        {
            return FormatBoundedEnumerable(
                enumerable,
                enumerable,
                depth,
                activeReferences);
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    private static string FormatBoundedDebugEntries(
        object collection,
        IEnumerable<KeyValuePair<string, object>> entries,
        int depth,
        HashSet<object> activeReferences)
    {
        if (depth >= MaxBoundedCollectionDepth)
            return BoundedCollectionDepthMarker;
        if (!activeReferences.Add(collection))
            return BoundedCollectionCycleMarker;

        try
        {
            var boundedEntries = entries
                .Take(MaxBoundedCollectionItems + 1)
                .ToArray();
            var renderedEntries = boundedEntries
                .Take(MaxBoundedCollectionItems)
                .Select(pair => $"{pair.Key}: {FormatBoundedDebugValue(pair.Value, depth + 1, activeReferences)}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToList();
            if (boundedEntries.Length > MaxBoundedCollectionItems)
                renderedEntries.Add(BoundedCollectionItemsMarker);

            return $"{{{string.Join(", ", renderedEntries)}}}";
        }
        finally
        {
            activeReferences.Remove(collection);
        }
    }

    private static string FormatBoundedNonGenericDictionary(
        System.Collections.IDictionary dictionary,
        int depth,
        HashSet<object> activeReferences)
    {
        if (depth >= MaxBoundedCollectionDepth)
            return BoundedCollectionDepthMarker;
        if (!activeReferences.Add(dictionary))
            return BoundedCollectionCycleMarker;

        try
        {
            var boundedKeys = dictionary.Keys
                .Cast<object?>()
                .Take(MaxBoundedCollectionItems + 1)
                .ToArray();
            if (boundedKeys.Any(key => key is not string))
            {
                activeReferences.Remove(dictionary);
                return FormatBoundedEnumerable(
                    dictionary,
                    dictionary,
                    depth,
                    activeReferences);
            }

            var entries = boundedKeys
                .Cast<string>()
                .Select(key => new KeyValuePair<string, object>(key, dictionary[key]!));
            var renderedEntries = entries
                .Take(MaxBoundedCollectionItems)
                .Select(pair => $"{pair.Key}: {FormatBoundedDebugValue(pair.Value, depth + 1, activeReferences)}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToList();
            if (boundedKeys.Length > MaxBoundedCollectionItems)
                renderedEntries.Add(BoundedCollectionItemsMarker);

            return $"{{{string.Join(", ", renderedEntries)}}}";
        }
        finally
        {
            activeReferences.Remove(dictionary);
        }
    }

    private static string FormatBoundedEnumerable(
        object collection,
        IEnumerable enumerable,
        int depth,
        HashSet<object> activeReferences)
    {
        if (depth >= MaxBoundedCollectionDepth)
            return BoundedCollectionDepthMarker;
        if (!activeReferences.Add(collection))
            return BoundedCollectionCycleMarker;

        try
        {
            var boundedItems = enumerable
                .Cast<object?>()
                .Take(MaxBoundedCollectionItems + 1)
                .ToArray();
            var renderedItems = boundedItems
                .Take(MaxBoundedCollectionItems)
                .Select(item => FormatBoundedDebugValue(item, depth + 1, activeReferences))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
            if (boundedItems.Length > MaxBoundedCollectionItems)
                renderedItems.Add(BoundedCollectionItemsMarker);

            return $"[{string.Join(", ", renderedItems)}]";
        }
        finally
        {
            activeReferences.Remove(collection);
        }
    }

    private static string FormatDebugEntries(
        IEnumerable<KeyValuePair<string, object>> entries)
    {
        return $"{{{string.Join(", ", entries
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}: {FormatDebugValue(pair.Value)}"))}}}";
    }
}
