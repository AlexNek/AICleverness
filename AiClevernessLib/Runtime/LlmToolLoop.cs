using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;

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
    private readonly ILlmErrorClassifier _errorClassifier;

    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILlmClient _llm;

    private readonly ILogger? _logger;

    private readonly IModelCatalog? _modelCatalog;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly AgentRuntimeOptions _options;

    private readonly IToolExecutor _toolExecutor;

    private readonly IToolRegistry _tools;

    public LlmToolLoop(
        ILlmClient llm,
        IToolRegistry tools,
        IToolExecutor toolExecutor,
        AgentRuntimeOptions options,
        IEnumerable<IAgentObserver> observers,
        IExecutionEventPublisher? eventPublisher,
        ILogger? logger,
        ILlmErrorClassifier? errorClassifier = null,
        IModelCatalog? modelCatalog = null)
    {
        _llm = llm;
        _tools = tools;
        _toolExecutor = toolExecutor;
        _options = options;
        _observers = observers;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _errorClassifier = errorClassifier ?? new DefaultLlmErrorClassifier();
        _modelCatalog = modelCatalog;
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

        var messages = new List<LlmMessage>(16);

        var systemPrompt = context.GetProperty<string>(AgentPropertyKeys.SystemPrompt)
                           ?? _options.DefaultSystemPrompt;
        var qualityFeedback = context.GetProperty<string>(AgentPropertyKeys.QualityFeedback);
        if (!string.IsNullOrWhiteSpace(qualityFeedback))
        {
            systemPrompt =
                $"{systemPrompt}{Environment.NewLine}{Environment.NewLine}Quality feedback from previous attempt:{Environment.NewLine}{qualityFeedback}";
        }

        messages.Add(new LlmMessage("system", systemPrompt));
        messages.Add(new LlmMessage("user", request.Goal));

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
        var options = new LlmCompletionOptions(temperature, null, model);

        // Set when a failover retry rewinds the loop to the same logical turn —
        // the retry must not increment the state turn counter or emit a second
        // TurnStartedEvent for that turn.
        var retryingSameTurn = false;

        // Failover handler — resolves the effective chain (validating explicit
        // names against the catalog) and manages candidate consumption.
        var failoverHandler = await ModelFailoverHandler.CreateAsync(
            context,
            _options,
            _observers,
            _eventPublisher,
            _logger,
            _modelCatalog,
            cancellationToken);

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
                        new LlmTokenUsage(totalPromptTokens, totalCompletionTokens));
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!retryingSameTurn)
            {
                executionContext.State.IncrementTurn();
                emit?.Invoke(new TurnStartedEvent { ExecutionId = executionId, Turn = turn });
            }

            retryingSameTurn = false;

            LlmResponse response;
            var llmCallStarted = DateTimeOffset.UtcNow;
            try
            {
                using var turnCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                turnCts.CancelAfter(TimeSpan.FromSeconds(completionTimeoutSeconds));
                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnLlmCalledAsync(messages, turnCts.Token),
                    _logger,
                    turnCts.Token);
                llmCallStarted = DateTimeOffset.UtcNow;
                response = await _llm.CompleteAsync(
                               messages,
                               toolDefinitions.Count > 0 ? toolDefinitions : null,
                               options,
                               turnCts.Token);
                var callDuration = DateTimeOffset.UtcNow - llmCallStarted;
                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnLlmRespondedAsync(
                        response,
                        callDuration,
                        turnCts.Token),
                    _logger,
                    turnCts.Token);

                // Emit OnLlmCallCompletedAsync — success.
                var successInfo = new LlmCallInfo
                {
                    ExecutionId = executionId,
                    Model = options.Model ?? "unknown",
                    Turn = turn,
                    Attempt = failoverHandler.Attempt,
                    IsFallback = failoverHandler.IsOnFallback,
                    Duration = callDuration,
                    Usage = response.Usage is not null
                        ? new LlmTokenUsage(response.Usage.PromptTokens, response.Usage.CompletionTokens)
                        : null,
                    Success = true,
                    StartedAt = llmCallStarted
                };
                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnLlmCallCompletedAsync(successInfo, cancellationToken),
                    _logger,
                    cancellationToken);

                // Publish LLM call completed event — success.
                if (_eventPublisher is not null)
                {
                    await _eventPublisher.PublishAsync(
                        new LlmCallCompletedBusEvent(
                            executionId,
                            callDuration,
                            response.Usage,
                            Success: true,
                            Turn: turn),
                        turnCts.Token);
                }
            }
            catch (OperationCanceledException ocEx) when (!cancellationToken.IsCancellationRequested)
            {
                var timeoutDuration = DateTimeOffset.UtcNow - llmCallStarted;
                var classification = _errorClassifier.Classify(ocEx, cancellationToken);
                var timeoutError =
                    $"LLM completion timed out after {completionTimeoutSeconds}s on turn {turn}";

                // Emit OnLlmCallCompletedAsync — timeout.
                var timeoutInfo = new LlmCallInfo
                {
                    ExecutionId = executionId,
                    Model = options.Model ?? "unknown",
                    Turn = turn,
                    Attempt = failoverHandler.Attempt,
                    IsFallback = failoverHandler.IsOnFallback,
                    Duration = timeoutDuration,
                    Success = false,
                    Error = timeoutError,
                    Classification = classification,
                    StartedAt = llmCallStarted
                };
                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnLlmCallCompletedAsync(timeoutInfo, cancellationToken),
                    _logger,
                    cancellationToken);

                // Publish LLM call completed event — timeout.
                if (_eventPublisher is not null)
                {
                    await _eventPublisher.PublishAsync(
                        new LlmCallCompletedBusEvent(
                            executionId,
                            timeoutDuration,
                            null,
                            Success: false,
                            Turn: turn,
                            Error: timeoutError),
                        cancellationToken);
                }

                // Failover path: advance to the next candidate when available.
                if (classification == FailureClassification.TransientAdvance)
                {
                    var nextOptions = await failoverHandler.TryFailoverAsync(
                        executionId,
                        turn,
                        options,
                        context,
                        timeoutError,
                        "timed out",
                        $"timeout after {completionTimeoutSeconds}s",
                        steps,
                        report,
                        emit,
                        cancellationToken);
                    if (nextOptions is not null)
                    {
                        options = nextOptions;

                        // Rewind turn counter — the failed attempt does not
                        // count against maxTurns, and the retry keeps the same
                        // logical turn in state and events.
                        turn--;
                        retryingSameTurn = true;
                        continue;
                    }
                }

                // Chain exhaustion or failover disabled — existing failure path.
                var errorPhase = failoverHandler.IsEnabled && classification == FailureClassification.TransientAdvance
                    ? "ModelFailover"
                    : "LlmCompletion";
                var exhaustionMsg = failoverHandler.IsEnabled && classification == FailureClassification.TransientAdvance
                    ? $"LLM failover chain exhausted after {failoverHandler.Attempt} attempts; last model tried: '{options.Model ?? "unknown"}' on turn {turn}"
                    : timeoutError;

                _logger?.LogWarning("Agent: {Message}", exhaustionMsg);
                steps.Add(exhaustionMsg);
                report(exhaustionMsg);
                emit?.Invoke(new FailureEvent
                                 {
                                     ExecutionId = executionId,
                                     Error = exhaustionMsg,
                                     Phase = errorPhase,
                                     IsTransient = errorPhase == "LlmCompletion"
                                 });
                var timeoutUsage = new LlmTokenUsage(totalPromptTokens, totalCompletionTokens);
                return new AgentResult(false, null, exhaustionMsg, steps, timeoutUsage);
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
                        new LlmTokenUsage(totalPromptTokens, totalCompletionTokens));
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LLM completion failed on turn {Turn}", turn);

                var classification = _errorClassifier.Classify(ex, cancellationToken);

                // Emit OnLlmCallCompletedAsync — error.
                var errorInfo = new LlmCallInfo
                {
                    ExecutionId = executionId,
                    Model = options.Model ?? "unknown",
                    Turn = turn,
                    Attempt = failoverHandler.Attempt,
                    IsFallback = failoverHandler.IsOnFallback,
                    Duration = DateTimeOffset.UtcNow - llmCallStarted,
                    Success = false,
                    Error = ex.Message,
                    Classification = classification,
                    StartedAt = llmCallStarted
                };
                await ObserverNotifier.NotifyAllAsync(
                    _observers,
                    observer => observer.OnLlmCallCompletedAsync(errorInfo, cancellationToken),
                    _logger,
                    cancellationToken);

                // Publish LLM call completed event — error.
                if (_eventPublisher is not null)
                {
                    await _eventPublisher.PublishAsync(
                        new LlmCallCompletedBusEvent(
                            executionId,
                            errorInfo.Duration,
                            null,
                            Success: false,
                            Turn: turn,
                            Error: ex.Message),
                        cancellationToken);
                }

                // Failover path: advance to the next candidate when available
                // (future: rate-limit signals via the classifier).
                if (classification == FailureClassification.TransientAdvance)
                {
                    var nextOptions = await failoverHandler.TryFailoverAsync(
                        executionId,
                        turn,
                        options,
                        context,
                        ex.Message,
                        "failed",
                        ex.Message,
                        steps,
                        report,
                        emit,
                        cancellationToken);
                    if (nextOptions is not null)
                    {
                        options = nextOptions;

                        // Rewind turn counter — the failed attempt does not
                        // count against maxTurns, and the retry keeps the same
                        // logical turn in state and events.
                        turn--;
                        retryingSameTurn = true;
                        continue;
                    }
                }

                var errorMsg = $"LLM error on turn {turn}: {ex.Message}";
                steps.Add(errorMsg);
                report(errorMsg);
                emit?.Invoke(new FailureEvent
                                 {
                                     ExecutionId = executionId,
                                     Error = ex.Message,
                                     Phase = "LlmCompletion",
                                     IsTransient = false
                                 });
                var errorUsage = new LlmTokenUsage(totalPromptTokens, totalCompletionTokens);
                return new AgentResult(false, null, ex.Message, steps, errorUsage);
            }

            if (response.Usage is not null)
            {
                totalPromptTokens += response.Usage.PromptTokens;
                totalCompletionTokens += response.Usage.CompletionTokens;
            }

            if (response.ToolCalls is { Count: > 0 })
            {
                messages.Add(new LlmMessage("assistant") { ToolCalls = response.ToolCalls });

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
                        messages.Add(new LlmMessage("tool", err) { ToolCallId = toolCall.Id });
                        continue;
                    }

                    var arguments = ToolCallArgumentParser.Parse(toolCall.Arguments);
                    var invocation = new ToolInvocation(toolCall.Name, arguments);

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
                        var result = await _toolExecutor.ExecuteAsync(
                                         tool,
                                         invocation,
                                         policy,
                                         cancellationToken);
                        var toolDuration = DateTimeOffset.UtcNow - started;
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

                        var output = result.Success
                                         ? result.Output ?? "(empty)"
                                         : $"Error: {result.Error ?? "unknown error"}";

                        var resultMsg = result.Success
                                            ? $"  {tool.Name} succeeded"
                                            : $"  {tool.Name} failed: {result.Error}";
                        steps.Add(resultMsg);
                        report(resultMsg);

                        messages.Add(new LlmMessage("tool", output) { ToolCallId = toolCall.Id });
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
                                new LlmTokenUsage(totalPromptTokens, totalCompletionTokens));
                        }

                        throw;
                    }
                }

                continue;
            }

            var content = response.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
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
                return new AgentResult(true, content, null, steps, usage);
            }

            var turnMsg = $"Turn {turn} produced no content and no tool calls.";
            steps.Add(turnMsg);
            report(turnMsg);
        }

        var exhausted = $"Exhausted {maxTurns} turns without a final response.";
        steps.Add(exhausted);
        report(exhausted);
        var finalUsage = new LlmTokenUsage(totalPromptTokens, totalCompletionTokens);
        return new AgentResult(false, null, exhausted, steps, finalUsage);
    }
}
