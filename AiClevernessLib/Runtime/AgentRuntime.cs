using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Middleware;
using AiCleverness.Runtime.Transcript;
using AiCleverness.Runtime.DecisionTree;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Default implementation of <see cref="IAgentRuntime"/> and <see cref="IStreamingAgentRuntime"/>.
/// Orchestrates policies, strategies, planning, and an LLM-driven tool loop
/// through a composable middleware pipeline.
/// </summary>
public sealed class AgentRuntime : IAgentRuntime, IStreamingAgentRuntime
{
    private readonly IExecutionEventPublisher? _eventPublisher;

    private readonly ILlmCompletionPipeline _completionPipeline;

    private readonly IEnumerable<IAgentInputValidator> _inputValidators;

    private readonly ILlmClient _llm;

    private readonly ILogger<AgentRuntime>? _logger;

    private readonly ILoggerFactory? _loggerFactory;

    private readonly IModelManager? _modelManager;

    private readonly IModelCatalog? _modelCatalog;

    private readonly IEnumerable<IAgentObserver> _observers;

    private readonly AgentRuntimeOptions _options;

    private readonly IAgentPlanner? _planner;

    private readonly IEnumerable<IAgentPolicy> _policies;

    private readonly IEnumerable<IAgentQualityGate> _qualityGates;

    private readonly IEnumerable<IAgentStrategy> _strategies;

    private readonly IToolExecutor _toolExecutor;

    private readonly IToolRegistry _tools;

    private readonly IEnumerable<IAgentResultTransformer> _transformers;

    private readonly IEnumerable<IAgentPipelineMiddleware> _userMiddleware;

    private readonly IEnumerable<IAgentResultValidator> _validators;

    public AgentRuntime(
        ILlmClient llm,
        IToolRegistry tools,
        IEnumerable<IAgentPolicy>? policies = null,
        IEnumerable<IAgentStrategy>? strategies = null,
        IAgentPlanner? planner = null,
        IToolExecutor? toolExecutor = null,
        IEnumerable<IAgentQualityGate>? qualityGates = null,
        IEnumerable<IAgentResultValidator>? validators = null,
        IEnumerable<IAgentResultTransformer>? transformers = null,
        IEnumerable<IAgentObserver>? observers = null,
        IEnumerable<IAgentPipelineMiddleware>? middleware = null,
        IEnumerable<IAgentInputValidator>? inputValidators = null,
        AgentRuntimeOptions? options = null,
        IExecutionEventPublisher? eventPublisher = null,
        IModelManager? modelManager = null,
        IModelCatalog? modelCatalog = null,
        ILoggerFactory? loggerFactory = null,
        ILlmCompletionPipeline? completionPipeline = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _toolExecutor = toolExecutor ?? new DefaultToolExecutor();
        _policies = policies ?? Array.Empty<IAgentPolicy>();
        _strategies = strategies ?? Array.Empty<IAgentStrategy>();
        _qualityGates = qualityGates ?? Array.Empty<IAgentQualityGate>();
        _validators = validators ?? Array.Empty<IAgentResultValidator>();
        _transformers = transformers ?? Array.Empty<IAgentResultTransformer>();
        _observers = observers ?? Array.Empty<IAgentObserver>();
        _userMiddleware = middleware ?? Array.Empty<IAgentPipelineMiddleware>();
        _inputValidators = inputValidators ?? Array.Empty<IAgentInputValidator>();
        _planner = planner;
        _options = options ?? new AgentRuntimeOptions();
        _eventPublisher = eventPublisher;
        _modelManager = modelManager;
        _modelCatalog = modelCatalog;
        _loggerFactory = loggerFactory;
        _completionPipeline = completionPipeline
            ?? new DefaultLlmCompletionPipeline(
                _llm,
                _observers,
                _eventPublisher,
                loggerFactory: _loggerFactory,
                modelCatalog: _modelCatalog);
        _logger = loggerFactory?.CreateLogger<AgentRuntime>();
    }

    public async Task<AgentResult> RunAsync(
        AgentRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (agentContext, executionContext) =
            await InitializeExecutionAsync(request, progress, cancellationToken);
        var transcript = executionContext.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript);

        try
        {
            // Build and run the pipeline.
            var pipeline = BuildPipeline(executionContext);
            var result = await pipeline(executionContext);

            return await FinalizeExecutionAsync(
                agentContext,
                executionContext,
                result,
                cancellationToken);
        }
        catch (OperationCanceledException cancellationException)
        {
            transcript?.CompleteException(cancellationException, "Cancelled");
            throw;
        }
        catch (Exception exception)
        {
            transcript?.CompleteException(exception, "Failed");
            throw;
        }
        finally
        {
            transcript?.FinalizeTranscript();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AgentEvent> RunStreamingAsync(
        AgentRequest request,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (agentContext, executionContext) =
            await InitializeExecutionAsync(request, null, cancellationToken);
        var transcript = executionContext.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript);
        var executionId = executionContext.Metadata.ExecutionId;
        var runStart = DateTimeOffset.UtcNow;

        try
        {
            // Stream pipeline events through an unbounded channel: the pipeline runs on
            // a background task while the async iterator drains the channel.
            var channel = Channel.CreateUnbounded<AgentEvent>();
            executionContext.Items.Set(
                ExecutionItemKeys.EventEmitter,
                (Action<AgentEvent>)(agentEvent => channel.Writer.TryWrite(agentEvent)));

            yield return new RunStartedEvent { ExecutionId = executionId, Request = request };

            var pipeline = BuildPipeline(executionContext);
            AgentResult? pipelineResult = null;
            Exception? pipelineFailure = null;
            var pipelineTask = Task.Run(
                async () =>
                {
                    try
                    {
                        pipelineResult = await pipeline(executionContext);
                    }
                    catch (Exception ex)
                    {
                        pipelineFailure = ex;
                    }
                    finally
                    {
                        channel.Writer.TryComplete();
                    }
                },
                CancellationToken.None);

            // Drain without the caller's token: the tool loop itself observes cancellation
            // and completes the writer, so the stream ends with a CancellationEvent.
            await foreach (var agentEvent in channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                yield return agentEvent;
            }

            await pipelineTask;
            if (pipelineFailure is not null)
            {
                transcript?.CompleteException(
                    pipelineFailure,
                    pipelineFailure is OperationCanceledException ? "Cancelled" : "Failed");
                ExceptionDispatchInfo.Capture(pipelineFailure).Throw();
            }

            // Post-hoc completion bookkeeping must not be cancellable: on cancellation
            // the token is already cancelled, and finalizing with it would turn observer
            // notifications / bus publishing into an OperationCanceledException instead
            // of a clean end of the stream.
            AgentResult finalResult;
            try
            {
                finalResult = await FinalizeExecutionAsync(
                    agentContext,
                    executionContext,
                    pipelineResult!,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                transcript?.CompleteException(ex, "Failed");
                throw;
            }

            yield return new RunCompletedEvent
                             {
                                 ExecutionId = executionId,
                                 Result = finalResult,
                                 Duration = DateTimeOffset.UtcNow - runStart
                             };
        }
        finally
        {
            transcript?.FinalizeTranscript();
        }
    }

    private AgentPipelineDelegate BuildPipeline(DefaultExecutionContext executionContext)
    {
        var builder = new AgentPipelineBuilder();

        // 1. Policy evaluation (outermost - can short-circuit before any work).
        builder.Use(new PolicyMiddleware(_policies, _observers, _logger, _eventPublisher));

        // 2. Input validation (after policies, before planning).
        builder.Use(new InputValidationMiddleware(_inputValidators, _logger));

        // 3. Planning.
        builder.Use(new PlanningMiddleware(_planner, _logger));

        // 4. Strategy execution (can short-circuit if strategy succeeds).
        builder.Use(new StrategyMiddleware(_strategies, _logger));

        // 5. User-registered middleware.
        builder.Use(_userMiddleware);

        // 6. Quality/validation/transformation wrapping the terminal.
        builder.Use(
            new QualityValidationTransformMiddleware(
                _qualityGates,
                _validators,
                _transformers,
                _observers,
                _logger,
                _eventPublisher));

        // Terminal: the LLM tool loop (constructed per execution).
        var loop = new LlmToolLoop(
            _completionPipeline,
            _tools,
            _toolExecutor,
            _options,
            _observers,
            _eventPublisher,
            _logger);
        builder.UseTerminal(loop.RunAsync);

        return builder.Build();
    }

    private async Task<AgentResult> FinalizeExecutionAsync(
        DefaultAgentContext agentContext,
        DefaultExecutionContext executionContext,
        AgentResult result,
        CancellationToken cancellationToken)
    {
        // Update legacy state for backward compatibility.
        var finalSteps = executionContext.Items.Get<List<string>>(ExecutionItemKeys.Steps)
                         ?? new List<string>();
        agentContext.State.Status = result.Success ? "Completed" :
                                    executionContext.State.Status == ExecutionStatus.Blocked
                                        ? "Blocked" :
                                        "Failed";
        agentContext.State.Set("usage", result.Usage);

        // Ensure steps are included in result if missing.
        if (result.Steps.Count == 0 && finalSteps.Count > 0)
        {
            result = result with { Steps = finalSteps };
        }

        var transcript = executionContext.Items.Get<TranscriptContext>(ExecutionItemKeys.Transcript);
        var transcriptExecutionStatus = result.Success
                                            ? "Completed"
                                            : executionContext.State.Status == ExecutionStatus.Blocked
                                                ? "Blocked"
                                                : result.FailureKind == EFailureKind.Cancelled
                                                    ? "Cancelled"
                                                    : result.FailureKind == EFailureKind.TurnLimitExceeded
                                                        ? "TurnLimitExceeded"
                                                        : "Failed";
        transcript?.Complete(result, transcriptExecutionStatus);
        transcript?.FinalizeTranscript();
        result = transcript?.ApplyMetadata(result) ?? result;

        // Notify completion (only if not already notified by policy middleware).
        if (executionContext.State.Status != ExecutionStatus.Blocked)
        {
            executionContext.State.MarkCompleted(
                result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed);
            await ObserverNotifier.NotifyAllAsync(
                _observers,
                observer => observer.OnRunCompletedAsync(
                    result,
                    agentContext,
                    cancellationToken),
                _logger,
                cancellationToken);

            // Publish execution completed event.
            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishAsync(
                    new ExecutionCompletedBusEvent(
                        executionContext.Metadata.ExecutionId,
                        result,
                        executionContext.State.Duration),
                    cancellationToken);
            }
        }

        return result;
    }

    private async Task<(DefaultAgentContext AgentContext, DefaultExecutionContext ExecutionContext)>
        InitializeExecutionAsync(
            AgentRequest request,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
    {
        var state = new AgentState { Status = "Running" };
        var agentContext = new DefaultAgentContext
                               {
                                   Goal = request.Goal,
                                   AgentName = request.AgentName ?? "default",
                                   State = state,
                                   Memory = new InMemoryAgentMemory()
                               };

        foreach (var parameter in request.Parameters)
        {
            agentContext.SetProperty(parameter.Key, parameter.Value);
        }

        // Create execution context.
        var availableToolNames = _tools.GetAvailableTools(agentContext)
            .Select(t => t.Name)
            .ToList();
        var executionContext = DefaultExecutionContext.Create(
            request,
            _options,
            agentContext,
            availableToolNames,
            cancellationToken);
        executionContext.State.MarkStarted();

        // Initialize shared steps list and progress reporter in execution items.
        executionContext.Items.Set(ExecutionItemKeys.Steps, new List<string>());
        executionContext.Items.Set(
            ExecutionItemKeys.Progress,
            (Action<string>)(message => progress?.Report(message)));

        var transcript = TranscriptContext.Create(
            request,
            executionContext.Metadata.ExecutionId,
            _options,
            _logger);
        executionContext.Items.Set(ExecutionItemKeys.Transcript, transcript);

        try
        {
            await ObserverNotifier.NotifyAllAsync(
                _observers,
                observer => observer.OnRunStartedAsync(request, agentContext, cancellationToken),
                _logger,
                cancellationToken);

            // Publish execution started event.
            if (_eventPublisher is not null)
            {
                await _eventPublisher.PublishAsync(
                    new ExecutionStartedBusEvent(executionContext.Metadata.ExecutionId, request),
                    cancellationToken);
            }

            // Resolve model profile from capability requirements if present.
            await ResolveModelProfileAsync(request, agentContext, cancellationToken);

            return (agentContext, executionContext);
        }
        catch (OperationCanceledException cancellationException)
        {
            transcript.CompleteException(cancellationException, "Cancelled");
            transcript.FinalizeTranscript();
            throw;
        }
        catch (Exception exception)
        {
            transcript.CompleteException(exception, "Failed");
            transcript.FinalizeTranscript();
            throw;
        }
    }

    private async Task<IReadOnlyList<CapabilityProfile>> ResolveModelProfileAsync(
        AgentRequest request,
        IAgentContext context,
        CancellationToken cancellationToken)
    {
        if (request.CapabilityRequirements is null || _modelManager is null)
        {
            return Array.Empty<CapabilityProfile>();
        }

        var result = await _modelManager.ResolveAsync(
                         request.CapabilityRequirements,
                         cancellationToken);

        if (result is not null)
        {
            context.SetProperty(
                AgentPropertyKeys.ModelExecutionInfo,
                new ModelExecutionInfo
                    {
                        Model = result.Model,
                        Profile = result.Profile,
                        Attempt = result.Attempts,
                        IsFallback = result.IsFallback,
                        RemainingFallbacks = result.Fallbacks.Count,
                        SelectionReason = result.SelectionReason
                    });
            context.SetProperty(AgentPropertyKeys.Model, result.Model.Name);

            // Store the full resolution result so ModelFailoverHandler can
            // access the Fallbacks chain at runtime.
            context.SetProperty(AgentPropertyKeys.ModelResolutionResult, result);

            _logger?.LogDebug(
                "Resolved model {ModelName} via profile {ProfileId} for agent request (fallbacks: {FallbackCount})",
                result.Model.Name,
                result.Profile.Id,
                result.Fallbacks.Count);
            return Array.Empty<CapabilityProfile>();
        }

        _logger?.LogWarning("Model resolution failed for request: {Goal}", request.Goal);
        return Array.Empty<CapabilityProfile>();
    }
}
