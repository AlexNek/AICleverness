using System.Globalization;
using System.Text;
using System.Text.Json;

using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Best-effort, per-execution Markdown transcript state.
/// </summary>
internal sealed class TranscriptContext
{
    private const int MaxFilenameAttempts = 100;

    private const int MaxTaskSlugLength = 80;

    private static readonly char[] InvalidFilenameCharacters = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "cookie",
        "set-cookie",
        "api-key",
        "access-token",
        "refresh-token",
        "client-secret",
        "password",
        "secret",
        "token"
    };

    private readonly object _gate = new();

    private readonly ILogger? _logger;

    private readonly Func<string, string>? _redactor;

    private readonly ITranscriptSink? _sink;

    private MarkdownTranscriptBuilder? _builder;

    private bool _completed;

    private bool _debugRuntimePromptWritten;

    private bool _terminalWritten;

    private TranscriptContext(
        ITranscriptSink? sink,
        Func<string, string>? redactor,
        bool debug,
        string? status,
        ILogger? logger)
    {
        _sink = sink;
        _redactor = redactor;
        Debug = debug;
        PersistenceStatus = status;
        _logger = logger;
        FilePath = sink?.FilePath;
    }

    public bool Debug { get; }

    public string? FilePath { get; }

    public bool HasMetadata => _sink is not null || PersistenceStatus is not null;

    public string? PersistenceStatus { get; private set; }

    private MarkdownTranscriptBuilder Builder => _builder ??= new();

    public static TranscriptContext Create(
        AgentRequest request,
        string executionId,
        AgentRuntimeOptions options,
        ILogger? logger)
    {
        var parameters = request.Parameters;
        var debug = parameters.TryGetValue(AgentPropertyKeys.MarkdownTranscriptDebug, out var debugValue)
                    && debugValue is bool debugEnabled
                    && debugEnabled;

        if (!parameters.TryGetValue(
                AgentPropertyKeys.MarkdownTranscriptDirectory,
                out var directoryValue)
            || directoryValue is not string directory
            || string.IsNullOrWhiteSpace(directory)
            || !Path.IsPathFullyQualified(directory))
        {
            var hasDirectory = parameters.ContainsKey(AgentPropertyKeys.MarkdownTranscriptDirectory);
            return Disabled(hasDirectory ? "Unavailable" : null, debug, logger, executionId);
        }

        if (!debug && options.TranscriptRedactor is null)
            return Disabled("RedactorUnavailable", debug, logger, executionId);

        try
        {
            var fullDirectory = Path.GetFullPath(directory);
            var startedAt = DateTimeOffset.UtcNow;
            var localTimestamp = startedAt.ToLocalTime();
            var filenameGoal = debug
                                   ? request.Goal
                                   : RedactGoalForFilename(request.Goal, options.TranscriptRedactor!);
            var fileNamePrefix =
                $"{localTimestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{CreateTaskSlug(filenameGoal)}";
            var sink = CreateFileSink(fullDirectory, fileNamePrefix);
            var context = new TranscriptContext(
                sink,
                options.TranscriptRedactor,
                debug,
                status: null,
                logger);
            context.Append(
                context.Builder.Header(
                    context.RedactText(request.Goal),
                    executionId,
                    startedAt,
                    debug));
            if (debug)
                context.AppendDebugRequest(parameters);

            return context;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Markdown transcript initialization failed for execution {ExecutionId}.",
                executionId);
            return new TranscriptContext(null, null, debug, "Unavailable", logger);
        }
    }

    public void AppendTurn(int turn, int qualityAttempt, int failoverAttempt, string? model)
    {
        if (_sink is null)
            return;

        Append(Builder.Turn(turn, qualityAttempt, failoverAttempt, model));
    }

    public void AppendDebugRequest(IReadOnlyDictionary<string, object> parameters)
    {
        if (!Debug || _sink is null)
            return;

        Append(Builder.DebugRequest(parameters));
    }

    public void AppendDebugRuntime(
        ExecutionMetadata metadata,
        ExecutionState state,
        ModelExecutionInfo? modelExecutionInfo,
        string systemPrompt,
        string? qualityFeedback,
        int maxTurns,
        float temperature,
        int completionTimeoutSeconds,
        int idleTimeoutSeconds,
        string? model)
    {
        if (!Debug || _sink is null)
            return;

        lock (_gate)
        {
            if (_completed || _sink is null || PersistenceStatus is not null)
                return;

            try
            {
                var includeSystemPrompt = !_debugRuntimePromptWritten;
                _sink.Append(
                    Builder.DebugRuntime(
                        metadata,
                        state,
                        modelExecutionInfo,
                        systemPrompt,
                        includeSystemPrompt,
                        qualityFeedback,
                        maxTurns,
                        temperature,
                        completionTimeoutSeconds,
                        idleTimeoutSeconds,
                        model));
                _debugRuntimePromptWritten = true;
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    public void AppendModelContent(string? content)
    {
        if (_sink is null || string.IsNullOrWhiteSpace(content))
            return;

        Append(Builder.ModelContent(RedactText(content)));
    }

    public void AppendToolDecision(
        string? model,
        string toolName,
        string? callId,
        string rawArguments)
    {
        if (_sink is null)
            return;

        Append(
            Builder.ToolDecision(
                model ?? "unknown",
                toolName,
                callId,
                RedactArguments(rawArguments)));
    }

    public void AppendToolResult(
        string toolName,
        string? callId,
        string status,
        string? output,
        string? error)
    {
        if (_sink is null)
            return;

        Append(
            Builder.ToolResult(
                toolName,
                callId,
                status,
                output is null ? null : RedactText(output),
                error is null ? null : RedactText(error)));
    }

    public void AppendRetry(string reason, int retryNumber)
    {
        if (_sink is null)
            return;

        Append(Builder.Retry(RedactText(reason), retryNumber));
    }

    public void AppendStatus(string status, string? detail = null)
    {
        if (_sink is null)
            return;

        Append(Builder.Status(status, detail is null ? null : RedactText(detail)));
    }

    public void AppendDecisionNode(
        string nodeId,
        EDecisionNodeType nodeType,
        TimeSpan duration,
        string? outcome)
    {
        if (_sink is null)
            return;

        Append(
            Builder.DecisionNode(
                nodeId,
                nodeType,
                duration,
                outcome is null ? null : RedactText(outcome)));
    }

    public void AppendDecisionAction(
        string nodeId,
        string actionName,
        DecisionActionStatus status,
        string? error)
    {
        if (_sink is null)
            return;

        Append(
            Builder.DecisionAction(
                nodeId,
                actionName,
                status,
                error is null ? null : RedactText(error)));
    }

    public void AppendDecisionQuestion(
        string nodeId,
        string answer,
        string? observation,
        string? confidence,
        int attempt)
    {
        if (_sink is null)
            return;

        Append(
            Builder.DecisionQuestion(
                nodeId,
                RedactText(answer),
                observation is null ? null : RedactText(observation),
                confidence is null ? null : RedactText(confidence),
                attempt));
    }

    public void CompleteDecision(DecisionTreeResult result)
    {
        lock (_gate)
        {
            if (_completed || _sink is null || _terminalWritten || PersistenceStatus is not null)
                return;

            try
            {
                var verdict = result.Verdict is null ? null : RedactText(result.Verdict);
                var error = result.Error is null ? null : RedactText(result.Error);
                if (PersistenceStatus is not null)
                    return;

                _sink.Append(
                    Builder.DecisionResult(
                        result.Outcome,
                        result.Succeeded,
                        verdict,
                        error,
                        result.Usage));
                _terminalWritten = true;
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    public void Complete(AgentResult result, string status)
    {
        lock (_gate)
        {
            if (_completed || _sink is null || _terminalWritten || PersistenceStatus is not null)
                return;

            try
            {
                var redactedResult = result with
                                     {
                                         Output = result.Output is null ? null : RedactText(result.Output),
                                         Reasoning = result.Reasoning is null ? null : RedactText(result.Reasoning)
                                     };
                if (PersistenceStatus is not null)
                    return;

                _sink.Append(Builder.Final(redactedResult, status));
                _terminalWritten = true;
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    public void RecordException(Exception exception, string status) =>
        CompleteException(exception, status);

    public void CompleteException(Exception exception, string status)
    {
        if (_sink is null)
            return;

        lock (_gate)
        {
            if (_completed || _terminalWritten)
                return;

            try
            {
                var detail = RedactText(exception.Message);
                if (PersistenceStatus is not null)
                    return;

                _sink.Append(Builder.Status(status, detail));
                _sink.Append(Builder.FinalFailure(status, detail));
                _terminalWritten = true;
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    public void FinalizeTranscript()
    {
        lock (_gate)
        {
            if (_completed)
                return;

            _completed = true;
            if (_sink is null)
                return;

            try
            {
                _sink.Complete();
                if (PersistenceStatus is null)
                    PersistenceStatus = "Completed";
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
                try
                {
                    _sink.Dispose();
                }
                catch (Exception disposeException)
                {
                    _logger?.LogDebug(
                        disposeException,
                        "Markdown transcript disposal failed for {TranscriptPath}.",
                        FilePath);
                }
            }
        }
    }

    public AgentResult ApplyMetadata(AgentResult result)
    {
        if (!HasMetadata)
            return result;

        var metadata = new Dictionary<string, object>(result.Metadata)
                       {
                           [AgentResultMetadataKeys.MarkdownTranscriptStatus] =
                               PersistenceStatus ?? "FinalizationFailed"
                       };
        if (PersistenceStatus == "Completed" && FilePath is not null)
            metadata[AgentResultMetadataKeys.MarkdownTranscriptPath] = FilePath;

        return result with { Metadata = metadata };
    }

    private static FileTranscriptSink CreateFileSink(string directory, string fileNamePrefix)
    {
        for (var attempt = 1; attempt <= MaxFilenameAttempts; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : $"-{attempt}";
            var filePath = Path.Combine(directory, $"{fileNamePrefix}{suffix}.md");
            try
            {
                return new FileTranscriptSink(filePath);
            }
            catch (IOException) when (File.Exists(filePath))
            {
                // A concurrent or earlier execution owns this human-readable name.
            }
        }

        throw new IOException(
            $"Could not create a unique transcript file after {MaxFilenameAttempts} attempts.");
    }

    private static string RedactGoalForFilename(
        string goal,
        Func<string, string> redactor)
    {
        var redacted = redactor(goal);
        return redacted ?? throw new InvalidOperationException(
            "Transcript redactor returned null for the task filename.");
    }

    private static string CreateTaskSlug(string goal)
    {
        var slug = new StringBuilder(Math.Min(goal.Length, MaxTaskSlugLength));
        var separatorPending = false;

        foreach (var character in goal)
        {
            if (Array.IndexOf(InvalidFilenameCharacters, character) >= 0
                || !char.IsLetterOrDigit(character))
            {
                if (slug.Length > 0)
                    separatorPending = true;

                continue;
            }

            if (slug.Length >= MaxTaskSlugLength)
                break;

            if (separatorPending)
            {
                slug.Append('-');
                separatorPending = false;
            }

            if (slug.Length >= MaxTaskSlugLength)
                break;

            slug.Append(character);
        }

        return slug.Length == 0 ? "task" : slug.ToString().TrimEnd('-');
    }

    private static TranscriptContext Disabled(
        string? status,
        bool debug,
        ILogger? logger,
        string executionId)
    {
        if (status is not null)
        {
            logger?.LogWarning(
                "Markdown transcript disabled for execution {ExecutionId}: {Reason}.",
                executionId,
                status);
        }

        return new TranscriptContext(null, null, debug, status, logger);
    }

    private void Append(string content)
    {
        lock (_gate)
        {
            if (_completed || _sink is null || PersistenceStatus is not null)
                return;

            try
            {
                _sink.Append(content);
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    private string RedactArguments(string rawArguments)
    {
        if (Debug)
            return rawArguments;

        string candidate;
        try
        {
            using var document = JsonDocument.Parse(rawArguments);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteRedactedJson(document.RootElement, writer);
            }

            var rendered = Encoding.UTF8.GetString(stream.ToArray());
            candidate = document.RootElement.ValueKind == JsonValueKind.Object
                            ? rendered
                            : $"[NON_OBJECT_ARGUMENTS]{Environment.NewLine}{rendered}";
        }
        catch (JsonException)
        {
            candidate = $"[UNPARSEABLE_ARGUMENTS]{Environment.NewLine}{rawArguments}";
        }

        return ApplyHostRedactor(candidate);
    }

    private string RedactText(string content)
    {
        if (Debug)
            return content;

        var candidate = content;
        try
        {
            using var document = JsonDocument.Parse(content);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteRedactedJson(document.RootElement, writer);
            }

            candidate = Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            // Plain text is still sent through the host redactor.
        }

        return ApplyHostRedactor(candidate);
    }

    private string ApplyHostRedactor(string content)
    {
        if (_redactor is null)
            return content;

        try
        {
            var redacted = _redactor(content);
            if (redacted is null)
                throw new InvalidOperationException("Transcript redactor returned null.");

            return redacted;
        }
        catch (Exception ex)
        {
            Disable("FinalizationFailed", ex);
            return "[REDACTED]";
        }
    }

    private void WriteRedactedJson(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (SensitiveKeys.Contains(property.Name))
                        writer.WriteStringValue("[REDACTED]");
                    else
                        WriteRedactedJson(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteRedactedJson(item, writer);

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private void Disable(string status, Exception exception)
    {
        PersistenceStatus = status;
        _logger?.LogWarning(
            exception,
            "Markdown transcript persistence disabled for {TranscriptPath}.",
            FilePath);
    }
}
