using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Shared completion boundary used by agent and decision-tree execution.</summary>
public sealed class DefaultLlmCompletionPipeline : ILlmCompletionPipeline
{
    private readonly IExecutionEventPublisher? _eventPublisher;
    private readonly ILlmClient _llm;
    private readonly ILlmErrorClassifier _errorClassifier;
    private readonly ILogger? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IModelCatalog? _modelCatalog;
    private readonly IEnumerable<IAgentObserver> _observers;
    private readonly ILlmCallStrategy _strategy;

    /// <summary>
    /// Creates a completion pipeline using optional application-owned failure mappings.
    /// </summary>
    /// <param name="llm">The LLM client used for completions.</param>
    /// <param name="observers">Optional observers notified during completion execution.</param>
    /// <param name="eventPublisher">Optional execution-event publisher.</param>
    /// <param name="logger">Optional pipeline logger.</param>
    /// <param name="modelCatalog">Optional model catalog used for failover.</param>
    /// <param name="classificationOptions">
    /// Optional application-owned provider error and status mappings.
    /// </param>
    /// <param name="loggerFactory">Optional logger factory used by the call strategy.</param>
    public DefaultLlmCompletionPipeline(
        ILlmClient llm,
        IEnumerable<IAgentObserver>? observers = null,
        IExecutionEventPublisher? eventPublisher = null,
        ILogger<DefaultLlmCompletionPipeline>? logger = null,
        IModelCatalog? modelCatalog = null,
        LlmFailureClassificationOptions? classificationOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _observers = observers ?? Array.Empty<IAgentObserver>();
        _eventPublisher = eventPublisher;
        _logger = logger;
        _modelCatalog = modelCatalog;
        _loggerFactory = loggerFactory;
        _errorClassifier = new DefaultLlmErrorClassifier(classificationOptions);
        _strategy = LlmCallStrategyFactory.Create(_llm, _loggerFactory);
    }

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
        => CompleteCoreAsync(request, executionContext: null, cancellationToken);

    public Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        LlmCompletionExecutionContext executionContext,
        CancellationToken cancellationToken = default)
        => CompleteCoreAsync(request, executionContext, cancellationToken);

    private async Task<LlmResponse> CompleteCoreAsync(
        LlmCompletionRequest request,
        LlmCompletionExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var runtimeOptions = executionContext?.RuntimeOptions ?? new AgentRuntimeOptions();
        var agentContext = executionContext?.AgentContext;
        var failover = agentContext is null
            ? null
            : await ModelFailoverHandler.CreateAsync(
                agentContext,
                runtimeOptions,
                _observers,
                _eventPublisher,
                _logger,
                _modelCatalog,
                cancellationToken);
        var currentOptions = request.Options;
        var attempt = 1;
        var steps = executionContext?.Steps ?? new List<string>();
        var report = executionContext?.Report ?? (_ => { });
        var emit = executionContext?.Emit;

        while (true)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await NotifyCalledAsync(request.Messages, cancellationToken);

            try
            {
                var response = await _strategy.CallAsync(
                    request.Messages,
                    executionContext?.Tools,
                    currentOptions,
                    new LlmCallStrategyOptions(
                        executionContext?.CompletionTimeoutSeconds ?? runtimeOptions.DefaultCompletionTimeoutSeconds,
                        executionContext?.IdleTimeoutSeconds ?? runtimeOptions.DefaultIdleTimeoutSeconds,
                        executionContext?.OnChunk),
                    cancellationToken);

                await NotifyRespondedAsync(response, stopwatch.Elapsed, cancellationToken);
                await NotifyCompletedAsync(
                    request,
                    currentOptions,
                    failover,
                    attempt,
                    stopwatch.Elapsed,
                    response.Usage,
                    error: null,
                    classification: null,
                    providerFailure: null,
                    startedAt,
                    cancellationToken);
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var classification = _errorClassifier.Classify(exception, cancellationToken);
                var providerFailure = LlmProviderFailureMetadata.FromException(exception);
                var timeout = exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
                if (providerFailure is not null)
                {
                    _logger?.LogWarning(
                        "LLM completion provider failure classified as {Classification}: provider={Provider}, code={ErrorCode}, status={StatusCode}, retryAfter={RetryAfter}",
                        classification,
                        providerFailure.Provider,
                        providerFailure.ErrorCode,
                        providerFailure.StatusCode,
                        providerFailure.RetryAfter);
                }

                await NotifyCompletedAsync(
                    request,
                    currentOptions,
                    failover,
                    attempt,
                    stopwatch.Elapsed,
                    usage: null,
                    exception.Message,
                    classification,
                    providerFailure,
                    startedAt,
                    cancellationToken);

                if (classification == EFailureClassification.TransientAdvance && failover is not null)
                {
                    var nextOptions = await failover.TryFailoverAsync(
                        request.ExecutionId,
                        request.Turn,
                        currentOptions ?? new LlmCompletionOptions(),
                        agentContext!,
                        exception.Message,
                        timeout ? "timed out" : "failed",
                        exception.Message,
                        providerFailure,
                        steps,
                        report,
                        emit,
                        cancellationToken);
                    if (nextOptions is not null)
                    {
                        currentOptions = nextOptions;
                        attempt++;
                        continue;
                    }
                }

                throw new LlmCompletionFailureException(
                    exception,
                    classification,
                    timeout,
                    failover?.IsEnabled == true);
            }
        }
    }

    private async Task NotifyCalledAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnLlmCalledAsync(messages, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "LLM observer failed before a completion attempt.");
            }
        }
    }

    private async Task NotifyRespondedAsync(
        LlmResponse response,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnLlmRespondedAsync(response, duration, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "LLM observer failed after a completion attempt.");
            }
        }
    }

    private async Task NotifyCompletedAsync(
        LlmCompletionRequest request,
        LlmCompletionOptions? options,
        ModelFailoverHandler? failover,
        int attempt,
        TimeSpan duration,
        LlmTokenUsage? usage,
        string? error,
        EFailureClassification? classification,
        LlmProviderFailureMetadata? providerFailure,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var info = new LlmCallInfo
        {
            ExecutionId = request.ExecutionId,
            Model = options?.Model ?? "unknown",
            Turn = request.Turn,
            Attempt = failover?.Attempt ?? attempt,
            IsFallback = failover?.IsOnFallback ?? attempt > 1,
            Duration = duration,
            Usage = usage,
            Success = error is null,
            Error = error,
            Classification = classification,
            ProviderFailure = providerFailure,
            StartedAt = startedAt
        };

        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnLlmCallCompletedAsync(info, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "LLM observer failed after a completion attempt.");
            }
        }

        if (_eventPublisher is null)
            return;
        try
        {
            await _eventPublisher.PublishAsync(
                new LlmCallCompletedBusEvent(
                    request.ExecutionId,
                    duration,
                    usage,
                    Success: error is null,
                    Turn: request.Turn,
                    Error: error)
                {
                    ProviderFailure = providerFailure
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "LLM completion event publication failed.");
        }
    }
}