using System.Globalization;
using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Transcript;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// The single LLM tool loop used as the terminal step of the agent pipeline.
/// Handles per-turn timeout/cancellation, token accounting, tool dispatch, and
/// steps/progress reporting. Constructed per execution (no shared mutable state).
/// When an event emitter is present in the execution items (streaming runs),
/// progress is additionally surfaced as <see cref="AgentEvent"/> items.
/// </summary>
internal sealed class LlmToolLoop
{
    private const string ReasoningPropertyName = "reasoning";
    private const int InitialMessageCapacity = 16;
    private const int MaxReasoningDisplayLength = 500;
    private const int MaxDecisionArgumentLength = 200;
    private const int MaxToolResultPreviewLength = 100;
    private const int FirstLineSplitCount = 2;
    private const int EllipsisCharacterCount = 3;
    private const string Ellipsis = "...";
    private const string ToolFailedStatus = "Failed";
    private const string ToolCachedStatus = "Cached";
    private const string ToolCachedFailureStatus = "CachedFailure";
    private const string ToolSucceededStatus = "Succeeded";

    private static readonly string[] PreferredArgumentKeys = ["url", "uri", "query", "path"];

    private readonly ILlmCompletionPipeline _completionPipeline;

    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILogger? _logger;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly AgentRuntimeOptions _options;

    private readonly IToolExecutor _toolExecutor;

    private readonly IToolRegistry _tools;

    public LlmToolLoop(
        ILlmCompletionPipeline completionPipeline,
        IToolRegistry tools,
        IToolExecutor toolExecutor,
        AgentRuntimeOptions options,
        IEnumerable<IAgentObserver> observers,
        IExecutionEventPublisher? eventPublisher,
        ILogger? logger)
    {
        _completionPipeline = completionPipeline ?? throw new ArgumentNullException(nameof(completionPipeline));
        _tools = tools;
        _toolExecutor = toolExecutor;
        _options = options;
        _observers = observers;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Runs the LLM tool loop until a final response, an error, or turn exhaustion.
    /// </summary>
    public async Task<AgentResult> RunAsync(IExecutionContext executionContext)
    {
        var request = executionContext.Metadata.Request;
        var context = executionContext.AgentContext;
        var cancellationToken = executionContext.CancellationToken;
        var steps = executionContext.Items.Get<List<string>>(ExecutionItemKeys.Steps)
                    ?? new List<string>();
        var report = executionContext.Items.Get<Action<string>>(ExecutionItemKeys.Progress)
                     ?? (_ => { });
        var emit = executionContext.Items.Get<Action<AgentEvent>>(ExecutionItemKeys.EventEmitter);
        var executionId = executionContext.Metadata.ExecutionId;
        var transcript = executionContext.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript);

        var messages = new List<LlmMessage>(InitialMessageCapacity);

        var systemPrompt = context.GetProperty<string>(AgentPropertyKeys.SystemPrompt)
                           ?? _options.DefaultSystemPrompt;
        var qualityFeedback = context.GetProperty<string>(AgentPropertyKeys.QualityFeedback);
        if (!string.IsNullOrWhiteSpace(qualityFeedback))
        {
            systemPrompt =
                $"{systemPrompt}{Environment.NewLine}{Environment.NewLine}Quality feedback from previous attempt:{Environment.NewLine}{qualityFeedback}";
        }

        messages.Add(new LlmMessage(LlmMessageRoles.System, systemPrompt));
        messages.Add(new LlmMessage(LlmMessageRoles.User, request.Goal));

        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;

        var maxTurns = context.GetProperty<int?>(AgentPropertyKeys.MaxTurns)
                       ?? _options.DefaultMaxTurns;
        var temperature = context.GetProperty<float?>(AgentPropertyKeys.Temperature)
                          ?? _options.DefaultTemperature;
        var execInfo =
            context.GetProperty<ModelExecutionInfo>(AgentPropertyKeys.ModelExecutionInfo);
        var model = execInfo?.Model.Name ?? context.GetProperty<string>(AgentPropertyKeys.Model);
        var completionTimeoutSeconds =
            context.GetProperty<int?>(AgentPropertyKeys.CompletionTimeoutSeconds)
            ?? _options.DefaultCompletionTimeoutSeconds;
        var idleTimeoutSeconds =
            context.GetProperty<int?>(AgentPropertyKeys.IdleTimeoutSeconds)
            ?? _options.DefaultIdleTimeoutSeconds;
        var options = new LlmCompletionOptions(temperature, null, model);
        transcript?.AppendDebugRuntime(
            executionContext.Metadata,
            executionContext.State,
            execInfo,
            systemPrompt,
            qualityFeedback,
            maxTurns,
            temperature,
            completionTimeoutSeconds,
            idleTimeoutSeconds,
            model);

        var completionAttempt = 1;

        // null = unrestricted (every registered tool); an explicit list — including
        // an empty one — restricts the run to exactly the named tools.
        var allowedToolNames = request.AllowedToolNames;
        var availableTools = _tools.GetAvailableTools(context);
        if (allowedToolNames is not null)
        {
            availableTools = availableTools
                .Where(t => allowedToolNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var toolDefinitions = availableTools
            .Select(t => t.Definition)
            .Where(d => d is not null)
            .ToList();

        _logger?.LogDebug(
            "Starting tool loop with {ToolCount} tools and up to {MaxTurns} turns",
            toolDefinitions.Count,
            maxTurns);

        for (var turn = 0; turn < maxTurns; turn++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (emit is not null)
                {
                    emit(new CancellationEvent
                             {
                                 ExecutionId = executionId, Reason = "Cancellation requested."
                             });
                    return new AgentResult(
                        false,
                        null,
                        "Cancellation requested.",
                        steps,
                        new LlmTokenUsage(totalPromptTokens, totalCompletionTokens),
                        FailureKind: EFailureKind.Cancelled);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            executionContext.State.IncrementTurn();
            emit?.Invoke(new TurnStartedEvent { ExecutionId = executionId, Turn = turn });

            transcript?.AppendTurn(
                turn + 1,
                executionContext.State.QualityRetryCount + 1,
                completionAttempt,
                options.Model);

            LlmResponse response;
            try
            {
                response = await _completionPipeline.CompleteAsync(
                    new LlmCompletionRequest(executionId, messages, options, turn),
                    new LlmCompletionExecutionContext(
                        context,
                        _options,
                        toolDefinitions.Count > 0 ? toolDefinitions : null,
                        completionTimeoutSeconds,
                        idleTimeoutSeconds,
                        content => emit?.Invoke(new ModelChunkEvent
                        {
                            ExecutionId = executionId,
                            Content = content,
                            Turn = turn,
                            IsFinal = false
                        }),
                        emit,
                        steps,
                        report),
                    cancellationToken);
                completionAttempt = 1;
                var currentModel = context.GetProperty<ModelExecutionInfo>(AgentPropertyKeys.ModelExecutionInfo)?.Model.Name
                                   ?? context.GetProperty<string>(AgentPropertyKeys.Model);
                if (!string.IsNullOrWhiteSpace(currentModel))
                    options = options with { Model = currentModel };
            }
            catch (LlmCompletionFailureException exception)
            {
                var errorMessage = exception.Timeout
                    ? $"{exception.Message} on turn {turn}"
                    : $"LLM error on turn {turn}: {exception.Message}";
                var phase = exception.FailoverEnabled
                             && exception.Classification == EFailureClassification.TransientAdvance
                    ? LlmFailurePhases.ModelFailover
                    : LlmFailurePhases.LlmCompletion;
                transcript?.AppendStatus(
                    exception.Timeout ? "LLM timeout" : "LLM failure",
                    errorMessage);
                steps.Add(errorMessage);
                report(errorMessage);
                emit?.Invoke(new FailureEvent
                {
                    ExecutionId = executionId,
                    Error = errorMessage,
                    Phase = phase,
                    IsTransient = exception.Classification == EFailureClassification.TransientAdvance,
                    ProviderFailure = exception.ProviderFailure
                });
                var failureKind = phase == LlmFailurePhases.ModelFailover
                    ? EFailureKind.FailoverExhausted
                    : exception.Timeout ? EFailureKind.LlmTimeout : EFailureKind.LlmError;
                return new AgentResult(
                    false,
                    null,
                    errorMessage,
                    steps,
                    new LlmTokenUsage(totalPromptTokens, totalCompletionTokens),
                    FailureKind: failureKind);
            }
            catch (OperationCanceledException)
            {
                if (emit is not null)
                {
                    emit(new CancellationEvent
                    {
                        ExecutionId = executionId,
                        Reason = "Cancellation requested during LLM call."
                    });
                    return new AgentResult(
                        false,
                        null,
                        "Cancellation requested during LLM call.",
                        steps,
                        new LlmTokenUsage(totalPromptTokens, totalCompletionTokens),
                        FailureKind: EFailureKind.Cancelled);
                }

                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "LLM completion failed on turn {Turn}", turn);
                transcript?.AppendStatus("LLM failure", exception.Message);
                var errorMessage = $"LLM error on turn {turn}: {exception.Message}";
                steps.Add(errorMessage);
                report(errorMessage);
                emit?.Invoke(new FailureEvent
                {
                    ExecutionId = executionId,
                    Error = exception.Message,
                    Phase = LlmFailurePhases.LlmCompletion,
                    IsTransient = false
                });
                return new AgentResult(
                    false,
                    null,
                    exception.Message,
                    steps,
                    new LlmTokenUsage(totalPromptTokens, totalCompletionTokens),
                    FailureKind: EFailureKind.LlmError);
            }

            if (response.Usage is not null)
            {
                totalPromptTokens += response.Usage.PromptTokens;
                totalCompletionTokens += response.Usage.CompletionTokens;
            }

            if (response.ToolCalls is { Count: > 0 })
            {
                transcript?.AppendModelContent(response.Content);

                // Surface the model's existing content before tool calls. When
                // content is a workflow JSON object with a top-level reasoning
                // string, show that field instead of the complete JSON envelope.
                var reasoningText = ExtractJsonReasoning(response.Content) ?? response.Content;
                if (!string.IsNullOrWhiteSpace(reasoningText))
                {
                    var displayText = TruncateReasoning(
                        reasoningText.Trim(),
                        MaxReasoningDisplayLength);
                    var reasoningMsg = $"  {displayText}";
                    steps.Add(reasoningMsg);
                    report(reasoningMsg);
                }

                messages.Add(new LlmMessage(LlmMessageRoles.Assistant) { ToolCalls = response.ToolCalls });

                foreach (var toolCall in response.ToolCalls)
                {
                    // Resolve only from the tools allowed for this run — a
                    // tool excluded by AllowedToolNames must never execute,
                    // even if the model names it anyway.
                    var tool = availableTools.FirstOrDefault(
                        t => string.Equals(t.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase));
                    if (tool is null)
                    {
                        var err = allowedToolNames is not null
                                      && !allowedToolNames.Contains(toolCall.Name, StringComparer.OrdinalIgnoreCase)
                                      ? $"Tool '{toolCall.Name}' is not allowed for this run."
                                      : $"Tool '{toolCall.Name}' is not registered.";
                        steps.Add(err);
                        transcript?.AppendToolDecision(
                            options.Model,
                            toolCall.Name,
                            toolCall.Id,
                            toolCall.Arguments);
                        transcript?.AppendToolResult(
                            toolCall.Name,
                            toolCall.Id,
                            ToolFailedStatus,
                            null,
                            err);
                        messages.Add(new LlmMessage(LlmMessageRoles.Tool, err) { ToolCallId = toolCall.Id });
                        continue;
                    }

                    transcript?.AppendToolDecision(
                        options.Model,
                        toolCall.Name,
                        toolCall.Id,
                        toolCall.Arguments);
                    var arguments = ToolCallArgumentParser.Parse(toolCall.Arguments);
                    var invocation = new ToolInvocation(toolCall.Name, arguments);

                    var decisionMsg =
                        $"  [{GetModelLabel(options.Model)}] Decision: {tool.Name} — {ExtractKeyArgument(arguments)}";
                    steps.Add(decisionMsg);
                    report(decisionMsg);

                    var cacheHit = false;
                    ToolResult result;
                    var toolDuration = TimeSpan.Zero;

                    if (_toolExecutor is ICacheAwareToolExecutor cacheAwareExecutor
                        && cacheAwareExecutor.TryGetCachedResult(
                            tool,
                            invocation,
                            out var cachedResult))
                    {
                        cacheHit = true;
                        result = cachedResult;
                    }
                    else
                    {
                        var stepMsg =
                            $"Calling tool {tool.Name}({JsonSerializer.Serialize(arguments, AiClevernessJsonContext.Default.DictionaryStringObject)})";
                        steps.Add(stepMsg);
                        report(stepMsg);
                        _logger?.LogDebug("Invoking tool {ToolName}", tool.Name);

                        emit?.Invoke(new ToolStartedEvent
                                         {
                                             ExecutionId = executionId,
                                             ToolName = tool.Name,
                                             Invocation = invocation
                                         });

                        try
                        {
                            executionContext.State.IncrementToolInvocation();
                            await ObserverNotifier.NotifyAllAsync(
                                _observers,
                                observer => observer.OnToolInvokedAsync(
                                    tool,
                                    invocation,
                                    cancellationToken),
                                _logger,
                                cancellationToken);

                            // Publish tool invoked event.
                            if (_eventPublisher is not null)
                            {
                                await _eventPublisher.PublishAsync(
                                    new ToolInvokedBusEvent(executionId, tool.Name, invocation),
                                    cancellationToken);
                            }

                            var started = DateTimeOffset.UtcNow;
                            var policy = ToolPolicyFactory.Create(tool, context, _options);
                            result = await _toolExecutor.ExecuteAsync(
                                             tool,
                                             invocation,
                                             policy,
                                             cancellationToken);
                            toolDuration = DateTimeOffset.UtcNow - started;
                            await ObserverNotifier.NotifyAllAsync(
                                _observers,
                                observer => observer.OnToolCompletedAsync(
                                    tool,
                                    result,
                                    toolDuration,
                                    cancellationToken),
                                _logger,
                                cancellationToken);

                            // Publish tool completed event.
                            if (_eventPublisher is not null)
                            {
                                await _eventPublisher.PublishAsync(
                                    new ToolCompletedBusEvent(
                                        executionId,
                                        tool.Name,
                                        result,
                                        toolDuration),
                                    cancellationToken);
                            }

                            emit?.Invoke(new ToolCompletedAgentEvent
                                             {
                                                 ExecutionId = executionId,
                                                 ToolName = tool.Name,
                                                 Result = result,
                                                 Duration = toolDuration
                                             });
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            if (emit is not null)
                            {
                                emit(new CancellationEvent
                                         {
                                             ExecutionId = executionId,
                                             Reason = "Cancellation requested during tool execution."
                                         });
                                return new AgentResult(
                                    false,
                                    null,
                                    "Cancellation requested during tool execution.",
                                    steps,
                                    new LlmTokenUsage(totalPromptTokens, totalCompletionTokens),
                                    FailureKind: EFailureKind.Cancelled);
                            }

                            throw;
                        }
                    }

                    var output = result.Success
                                     ? result.Output ?? "(empty)"
                                     : $"Error: {result.Error ?? "unknown error"}";

                    var resultMsg = result.Success
                                        ? cacheHit
                                            ? $"  {tool.Name} reused cached result{FormatToolResultSummary(result.Output)}"
                                            : $"  {tool.Name} succeeded{FormatToolResultSummary(result.Output)}"
                                        : cacheHit
                                            ? $"  {tool.Name} reused cached failure: {result.Error}"
                                            : $"  {tool.Name} failed: {result.Error}";
                    steps.Add(resultMsg);
                    report(resultMsg);
                    transcript?.AppendToolResult(
                        tool.Name,
                        toolCall.Id,
                        cacheHit ? result.Success ? ToolCachedStatus : ToolCachedFailureStatus : result.Success ? ToolSucceededStatus : ToolFailedStatus,
                        result.Success ? result.Output : null,
                        result.Success ? null : result.Error);

                    messages.Add(new LlmMessage(LlmMessageRoles.Tool, output) { ToolCallId = toolCall.Id });
                }

                continue;
            }

            var content = response.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                transcript?.AppendModelContent(content);
                var finalMsg = "LLM returned final response.";
                steps.Add(finalMsg);
                report(finalMsg);
                emit?.Invoke(new ModelChunkEvent
                                 {
                                     ExecutionId = executionId,
                                     Content = content,
                                     Turn = turn,
                                     IsFinal = true
                                 });
                var usage = new LlmTokenUsage(totalPromptTokens, totalCompletionTokens);
                return new AgentResult(true, content, null, steps, usage, FailureKind: EFailureKind.NoFailure);
            }

            var turnMsg = $"Turn {turn} produced no content and no tool calls.";
            steps.Add(turnMsg);
            report(turnMsg);
        }

        var exhausted = $"Exhausted {maxTurns} turns without a final response.";
        transcript?.AppendStatus(LegacyExecutionStatuses.TurnLimitExceeded, exhausted);
        steps.Add(exhausted);
        report(exhausted);
        var finalUsage = new LlmTokenUsage(totalPromptTokens, totalCompletionTokens);
        return new AgentResult(false, null, exhausted, steps, finalUsage, FailureKind: EFailureKind.TurnLimitExceeded);
    }

    private static string ExtractKeyArgument(IReadOnlyDictionary<string, object> arguments)
    {
        foreach (var key in PreferredArgumentKeys)
        {
            if (arguments.TryGetValue(key, out var preferredValue)
                && IsScalar(preferredValue))
            {
                return FormatArgumentValue(preferredValue);
            }
        }

        foreach (var pair in arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (IsScalar(pair.Value))
            {
                return FormatArgumentValue(pair.Value);
            }
        }

        return "(no scalar argument)";
    }

    private static string? ExtractJsonReasoning(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(ReasoningPropertyName, out var reasoning)
                && reasoning.ValueKind == JsonValueKind.String)
            {
                return reasoning.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON model content is valid and remains the displayed fallback.
        }

        return null;
    }

    private static string FormatArgumentValue(object value)
    {
        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        text ??= string.Empty;
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        text = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{TruncateWithEllipsis(text, MaxDecisionArgumentLength)}\"";
    }

    private static string FormatToolResultSummary(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var firstLine = output.Split(
                new[] { '\r', '\n' },
                FirstLineSplitCount,
                StringSplitOptions.None)[0]
            .Trim();
        if (firstLine.Length == 0)
        {
            return string.Empty;
        }

        return $": {TruncateWithEllipsis(firstLine, MaxToolResultPreviewLength)}";
    }

    private static string GetModelLabel(string? model)
    {
        return string.IsNullOrWhiteSpace(model) ? "LLM" : model.Trim();
    }

    private static bool IsScalar(object? value)
    {
        return value is string
            or bool
            or byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal;
    }

    private static string TruncateReasoning(string text, int maxLength)
    {
        return text.Length > maxLength
            ? string.Concat(text.AsSpan(0, maxLength), Ellipsis)
            : text;
    }

    private static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= EllipsisCharacterCount)
        {
            return new string('.', maxLength);
        }

        return string.Concat(text.AsSpan(0, maxLength - EllipsisCharacterCount), Ellipsis);
    }

}
