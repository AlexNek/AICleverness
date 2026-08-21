using System.Globalization;
using System.Text;
using System.Text.Json;

using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Best-effort, per-execution Markdown transcript state.
/// </summary>
internal sealed class TranscriptContext
{
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

    private readonly MarkdownTranscriptBuilder _builder = new();

    private readonly object _gate = new();

    private readonly ILogger? _logger;

    private readonly Func<string, string>? _redactor;

    private readonly ITranscriptSink? _sink;

    private bool _completed;

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
            return Disabled(hasDirectory ? "Unavailable" : null, debug, logger);
        }

        if (!debug && options.TranscriptRedactor is null)
            return Disabled("RedactorUnavailable", debug, logger);

        try
        {
            var fullDirectory = Path.GetFullPath(directory);
            var fileName =
                $"{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}-{executionId}.md";
            var filePath = Path.Combine(fullDirectory, fileName);
            var sink = new FileTranscriptSink(filePath);
            var context = new TranscriptContext(
                sink,
                options.TranscriptRedactor,
                debug,
                status: null,
                logger);
            context.Append(
                context._builder.Header(
                    context.RedactText(request.Goal),
                    executionId,
                    DateTimeOffset.UtcNow,
                    debug));
            return context;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Markdown transcript initialization failed for execution {ExecutionId}.",
                executionId);
            return Disabled("Unavailable", debug, logger);
        }
    }

    public void AppendTurn(int turn, int attempt, string? model) =>
        Append(_builder.Turn(turn, attempt, model));

    public void AppendModelContent(string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
            Append(_builder.ModelContent(RedactText(content)));
    }

    public void AppendToolDecision(string? model, string toolName, string rawArguments)
    {
        Append(
            _builder.ToolDecision(
                model ?? "unknown",
                toolName,
                RedactArguments(rawArguments)));
    }

    public void AppendToolResult(
        string toolName,
        string status,
        string? output,
        string? error)
    {
        Append(
            _builder.ToolResult(
                toolName,
                status,
                output is null ? null : RedactText(output),
                error is null ? null : RedactText(error)));
    }

    public void AppendRetry(string reason, int retryNumber) =>
        Append(_builder.Retry(RedactText(reason), retryNumber));

    public void AppendStatus(string status, string? detail = null) =>
        Append(_builder.Status(status, detail is null ? null : RedactText(detail)));

    public void Complete(AgentResult result, string status)
    {
        lock (_gate)
        {
            if (_completed || _sink is null || _terminalWritten)
                return;

            try
            {
                _sink.Append(_builder.Final(
                    result with
                    {
                        Output = result.Output is null ? null : RedactText(result.Output),
                        Reasoning = result.Reasoning is null ? null : RedactText(result.Reasoning)
                    },
                    status));
                _terminalWritten = true;
            }
            catch (Exception ex)
            {
                Disable("FinalizationFailed", ex);
            }
        }
    }

    public void RecordException(Exception exception, string status)
    {
        if (_sink is null)
            return;

        AppendStatus(status, exception.Message);
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

    private static TranscriptContext Disabled(
        string? status,
        bool debug,
        ILogger? logger) =>
        new(null, null, debug, status, logger);

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

            candidate = Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            candidate = "[REDACTED_UNPARSEABLE_ARGUMENTS]";
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
