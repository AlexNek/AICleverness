using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Default completion boundary used by decision questions.</summary>
public sealed class DefaultLlmCompletionPipeline : ILlmCompletionPipeline
{
    private readonly IExecutionEventPublisher? _eventPublisher;
    private readonly ILlmClient _llm;
    private readonly ILogger? _logger;
    private readonly IEnumerable<IAgentObserver> _observers;

    public DefaultLlmCompletionPipeline(
        ILlmClient llm,
        IEnumerable<IAgentObserver>? observers = null,
        IExecutionEventPublisher? eventPublisher = null,
        ILogger<DefaultLlmCompletionPipeline>? logger = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _observers = observers ?? Array.Empty<IAgentObserver>();
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnLlmCalledAsync(request.Messages, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Decision observer failed before an LLM call.");
            }
        }

        LlmResponse? response = null;
        Exception? failure = null;
        try
        {
            response = await _llm.CompleteAsync(
                request.Messages,
                tools: null,
                request.Options,
                cancellationToken);
            foreach (var observer in _observers)
            {
                try
                {
                    await observer.OnLlmRespondedAsync(response, stopwatch.Elapsed, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Decision observer failed after an LLM call.");
                }
            }
            return response;
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var info = new LlmCallInfo
            {
                ExecutionId = request.ExecutionId,
                Model = request.Options?.Model ?? "unknown",
                Turn = request.Turn,
                Attempt = 1,
                IsFallback = false,
                Duration = stopwatch.Elapsed,
                Usage = response?.Usage,
                Success = failure is null,
                Error = failure?.Message,
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
                    _logger?.LogWarning(exception, "Decision observer failed after an LLM attempt.");
                }
            }

            if (_eventPublisher is not null)
            {
                try
                {
                    await _eventPublisher.PublishAsync(
                        new LlmCallCompletedBusEvent(
                            request.ExecutionId,
                            stopwatch.Elapsed,
                            response?.Usage,
                            failure is null,
                            request.Turn,
                            failure?.Message),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Decision LLM event handler failed.");
                }
            }
        }
    }
}
