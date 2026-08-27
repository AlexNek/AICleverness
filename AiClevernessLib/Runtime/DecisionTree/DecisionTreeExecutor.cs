using System.Diagnostics;
using System.Text.Json;
using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.Conversation;
using AiCleverness.Runtime.Transcript;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Executes validated decision trees with isolated per-run state.</summary>
public sealed class DecisionTreeExecutor
{
    private readonly IReadOnlyDictionary<string, IDecisionAction> _actions;
    private readonly IReadOnlyDictionary<string, IDecisionPredicate> _predicates;
    private readonly IDecisionDataPolicy _decisionDataPolicy;
    private readonly IDecisionLlmContextBuilder _contextBuilder;
    private readonly ILlmCompletionPipeline _completionPipeline;
    private readonly IConversationManager _conversationManager;
    private readonly IConversationManagerFactory? _conversationManagerFactory;
    private readonly IExecutionEventPublisher? _eventPublisher;
    private readonly IExecutionJournal _journal;
    private readonly EnumAnswerParser _answerParser;
    private readonly IDecisionTreeLoader _treeLoader;
    private readonly DecisionTreeExecutionOptions? _defaultOptions;
    private readonly AsyncLocal<TranscriptContext?> _transcript = new();

    public DecisionTreeExecutor(
        ILlmCompletionPipeline completionPipeline,
        IConversationManager conversationManager,
        IExecutionJournal journal,
        IExecutionEventPublisher? eventPublisher,
        IEnumerable<IDecisionAction> actions,
        IEnumerable<IDecisionPredicate> predicates,
        IDecisionLlmContextBuilder contextBuilder,
        IDecisionTreeLoader treeLoader,
        DecisionTreeExecutionOptions? defaultOptions = null,
        IConversationManagerFactory? conversationManagerFactory = null,
        IDecisionDataPolicy? decisionDataPolicy = null)
    {
        _completionPipeline = completionPipeline ?? throw new ArgumentNullException(nameof(completionPipeline));
        _conversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _eventPublisher = eventPublisher;
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _treeLoader = treeLoader ?? throw new ArgumentNullException(nameof(treeLoader));
        _conversationManagerFactory = conversationManagerFactory;
        _defaultOptions = defaultOptions;
        _defaultOptions?.DecisionDataPolicy.Validate();
        _defaultOptions?.DecisionTranscriptPolicy.Validate();
        _decisionDataPolicy = decisionDataPolicy
            ?? new DefaultDecisionDataPolicy(_defaultOptions?.DecisionDataPolicy);
        _answerParser = new EnumAnswerParser();
        _actions = BuildCatalog(actions);
        _predicates = BuildCatalog(predicates);
    }

    public async Task<DecisionTreeResult> ExecuteAsync(
        DecisionTreeModel tree,
        IReadOnlyDictionary<string, string>? templateParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var executionId = Guid.NewGuid().ToString("N");
        var state = new DecisionState();
        var data = new DataStore();
        var stopwatch = Stopwatch.StartNew();
        var parameters = templateParameters ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var transcript = CreateTranscript(tree, executionId);
        _transcript.Value = transcript;

        try
        {
            tree = _treeLoader.Validate(tree);
            _transcript.Value?.AppendDecisionOverview(tree);

            var budget = ApplyDefaults(tree.Budget ?? new DecisionBudget());
            var limits = new ResourceLimits
            {
                MaxNodeVisits = budget.MaxNodeVisits,
                MaxLlmCalls = budget.MaxLlmCalls,
                MaxDuration = budget.MaxElapsedTime,
                OnExceeded = budget.OnExceeded
            };
            var conversation = CreateConversationManager();
            var completionContext = DecisionTreeCompletionContextFactory.Create(_defaultOptions);
            var currentNodeId = tree.StartNodeId;
            var actionFailed = false;
            var unknown = false;
            string? executionError = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateDuration(state.ResourceUsage, stopwatch);
                if (await LimitExceededAsync(state.ResourceUsage, limits, cancellationToken))
                    return CreateResult(executionId, state, DecisionTreeOutcome.BudgetExhausted, null, "Decision resource budget exhausted.");

                var node = GetNode(tree, currentNodeId);
                var nodeStarted = stopwatch.Elapsed;
                if (state.ResourceUsage.NodeVisits >= budget.MaxNodeVisits
                    && await LimitReachedAsync(budget.MaxNodeVisits, state.ResourceUsage.NodeVisits, budget.OnExceeded, cancellationToken))
                    return CreateResult(executionId, state, DecisionTreeOutcome.BudgetExhausted, null, "Maximum decision node visits exceeded.");
                state.ResourceUsage.RecordNodeVisit();
                UpdateDuration(state.ResourceUsage, stopwatch);
                if (await LimitExceededAsync(state.ResourceUsage, limits, cancellationToken))
                {
                    await EmitNodeVisitedAsync(executionId, currentNodeId, node, stopwatch.Elapsed - nodeStarted, "budget", null, cancellationToken);
                    return CreateResult(executionId, state, DecisionTreeOutcome.BudgetExhausted, null, "Maximum decision node visits exceeded.");
                }

                string? outcome = null;
                string nextNodeId;
                switch (node.Type)
                {
                    case EDecisionNodeType.Action:
                    {
                        var action = GetAction(node.ActionName, currentNodeId);
                        var dataCountBeforeAction = data.GetAll().Count;
                        DecisionActionResult actionResult;
                        try
                        {
                            actionResult = await action.ExecuteAsync(
                                new DecisionActionContext(currentNodeId, executionId, parameters, state, data),
                                cancellationToken);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            actionResult = new DecisionActionResult(
                                null,
                                null,
                                DecisionActionStatus.PermanentFailure,
                                exception.Message);
                        }

                        foreach (var produced in actionResult.ProducedData ?? Array.Empty<DecisionData>())
                            data.Add(produced);
                        foreach (var property in actionResult.Properties ?? new Dictionary<string, string>())
                            state.Properties[property.Key] = property.Value;
                        if (actionResult.Status != DecisionActionStatus.Success)
                        {
                            actionFailed = true;
                            executionError ??= actionResult.Error;
                        }
                        outcome = actionResult.Status switch
                        {
                            DecisionActionStatus.Success => "success",
                            DecisionActionStatus.TransientFailure => "transientFailure",
                            DecisionActionStatus.PermanentFailure => "permanentFailure",
                            _ => throw new InvalidOperationException("Unsupported decision action status.")
                        };
                        await EmitActionCompletedAsync(
                            executionId,
                            currentNodeId,
                            node.ActionName!,
                            actionResult,
                            data.GetAll().Skip(dataCountBeforeAction).ToArray(),
                            cancellationToken);
                        nextNodeId = FindTransition(node, outcome).NextNodeId;
                        break;
                    }
                    case EDecisionNodeType.Classify:
                    {
                        if (state.ResourceUsage.LlmCalls >= budget.MaxLlmCalls
                            && await LimitReachedAsync(budget.MaxLlmCalls, state.ResourceUsage.LlmCalls, budget.OnExceeded, cancellationToken))
                            return CreateResult(executionId, state, DecisionTreeOutcome.BudgetExhausted, null, "Maximum decision LLM calls exceeded.");

                        var classification = await RunClassificationAsync(
                            executionId,
                            tree,
                            currentNodeId,
                            node,
                            state,
                            data,
                            parameters,
                            conversation,
                            budget,
                            limits,
                            stopwatch,
                            completionContext,
                            cancellationToken);
                        if (classification.BudgetExhausted)
                            return CreateResult(executionId, state, DecisionTreeOutcome.BudgetExhausted, null, "Decision resource budget exhausted.");
                        var answer = classification.Answer;
                        if (answer is null)
                        {
                            unknown = true;
                            var rawSnippet = TruncateForDisplay(classification.LastRawContent);
                            var parseDetail = rawSnippet is not null
                                ? $" Model returned: \"{rawSnippet}\""
                                : string.Empty;
                            executionError ??= classification.Error
                                ?? $"Classification response could not be classified.{parseDetail}";
                            var unknownAnswer = new EnumAnswer("unknown", executionError, null);
                            state.Classifications.Add(
                                new DecisionClassification(
                                    currentNodeId,
                                    unknownAnswer.Value,
                                    unknownAnswer.Observation,
                                    unknownAnswer.Confidence,
                                    DateTimeOffset.UtcNow));
                            var unknownTransition = FindTransition(node, "unknown");
                            await EmitClassificationCompletedAsync(
                                executionId,
                                currentNodeId,
                                unknownAnswer,
                                classification.Attempt,
                                cancellationToken);
                            outcome = "unknown";
                            nextNodeId = unknownTransition.NextNodeId;
                        }
                        else
                        {
                            state.Classifications.Add(
                                new DecisionClassification(
                                    currentNodeId,
                                    answer.Value,
                                    answer.Observation,
                                    answer.Confidence,
                                    DateTimeOffset.UtcNow));
                            await EmitClassificationCompletedAsync(
                                executionId,
                                currentNodeId,
                                answer,
                                classification.Attempt,
                                cancellationToken);
                            outcome = answer.Value;
                            nextNodeId = FindTransition(node, outcome).NextNodeId;
                        }
                        break;
                    }
                    case EDecisionNodeType.Condition:
                    {
                        var predicate = GetPredicate(node.PredicateName, currentNodeId);
                        bool result;
                        try
                        {
                            result = predicate.Evaluate(
                                new DecisionPredicateContext(
                                    currentNodeId,
                                    state,
                                    data,
                                    node.PredicateParameters ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal)));
                        }
                        catch (Exception exception)
                        {
                            await EmitNodeVisitedAsync(
                                executionId,
                                currentNodeId,
                                node,
                                stopwatch.Elapsed - nodeStarted,
                                "validationFailed",
                                null,
                                cancellationToken);
                            return CreateResult(
                                executionId,
                                state,
                                DecisionTreeOutcome.ValidationFailed,
                                null,
                                $"Predicate '{node.PredicateName}' failed: {exception.Message}");
                        }
                        outcome = result ? "true" : "false";
                        nextNodeId = FindTransition(node, outcome).NextNodeId;
                        break;
                    }
                    case EDecisionNodeType.Terminal:
                        await EmitNodeVisitedAsync(
                            executionId,
                            currentNodeId,
                            node,
                            stopwatch.Elapsed - nodeStarted,
                            node.Verdict,
                            null,
                            cancellationToken);
                        var finalOutcome = actionFailed
                            ? DecisionTreeOutcome.ActionFailed
                            : unknown ? DecisionTreeOutcome.Unknown : DecisionTreeOutcome.Terminal;
                        return CreateResult(executionId, state, finalOutcome, node.Verdict, executionError);
                    default:
                        return CreateResult(executionId, state, DecisionTreeOutcome.ValidationFailed, null, "Unsupported decision node type.");
                }

                await EmitNodeVisitedAsync(
                    executionId,
                    currentNodeId,
                    node,
                    stopwatch.Elapsed - nodeStarted,
                    outcome,
                    nextNodeId,
                    cancellationToken);
                currentNodeId = nextNodeId;
            }
        }
        catch (OperationCanceledException)
        {
            UpdateDuration(state.ResourceUsage, stopwatch);
            return CreateResult(executionId, state, DecisionTreeOutcome.Cancelled, null, "Decision execution was cancelled.");
        }
        catch (Exception exception)
        {
            UpdateDuration(state.ResourceUsage, stopwatch);
            return CreateResult(executionId, state, DecisionTreeOutcome.ValidationFailed, null, exception.Message);
        }
        finally
        {
            transcript?.FinalizeTranscript();
            _transcript.Value = null;
        }
    }

    private async Task<(EnumAnswer? Answer, int Attempt, bool BudgetExhausted, string? LastRawContent, string? Error)> RunClassificationAsync(
        string executionId,
        DecisionTreeModel tree,
        string nodeId,
        DecisionNode node,
        DecisionState state,
        DataStore data,
        IReadOnlyDictionary<string, string> parameters,
        IConversationManager conversation,
        DecisionBudget budget,
        ResourceLimits limits,
        Stopwatch stopwatch,
        LlmCompletionExecutionContext? completionContext,
        CancellationToken cancellationToken)
    {
        string? lastRawContent = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            if (attempt > 1 && state.ResourceUsage.LlmCalls >= budget.MaxLlmCalls
                && await LimitReachedAsync(budget.MaxLlmCalls, state.ResourceUsage.LlmCalls, budget.OnExceeded, cancellationToken))
                return (null, attempt, true, lastRawContent, null);
            cancellationToken.ThrowIfCancellationRequested();
            var selectedData = _decisionDataPolicy.Select(
                data.GetAll(),
                new DecisionDataSelectionContext(tree, node, state, parameters));
            var boundedData = new DecisionDataSnapshot(selectedData.Items);
            var built = _contextBuilder.Build(tree, node, state, boundedData, parameters);
            var requiredUserMessages = built
                .Where(message => string.Equals(message.Role, "user", StringComparison.Ordinal))
                .ToArray();
            conversation.AddMessages(built);
            var messages = await conversation.GetMessagesForCompletionAsync(
                budget.MaxContextTokens,
                cancellationToken);
            if (requiredUserMessages.Length == 0
                || requiredUserMessages.Any(requiredMessage =>
                    !messages.Any(message => ReferenceEquals(message, requiredMessage))))
            {
                return (
                    null,
                    attempt,
                    false,
                    lastRawContent,
                    $"Classification context exceeded MaxContextTokens ({budget.MaxContextTokens}); the required user input was omitted.");
            }

            var request = new LlmCompletionRequest(
                executionId,
                messages,
                new LlmCompletionOptions(
                    0.1f,
                    null,
                    completionContext is null
                        ? null
                        : completionContext.AgentContext?.GetProperty<string>(AgentPropertyKeys.Model)
                            ?? _defaultOptions!.Model),
                attempt);
            var response = completionContext is null
                ? await _completionPipeline.CompleteAsync(request, cancellationToken)
                : await _completionPipeline.CompleteAsync(request, completionContext, cancellationToken);
            _transcript.Value?.AppendDecisionLlmAttempt(nodeId, attempt, messages, response);
            var usage = response.Usage;
            state.ResourceUsage.RecordLlmUsage(usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0);
            UpdateDuration(state.ResourceUsage, stopwatch);
            if (await LimitExceededAsync(state.ResourceUsage, limits, cancellationToken))
                return (null, attempt, true, response.Content, null);
            conversation.AddMessage(new LlmMessage("assistant", response.Content));
            var parsed = _answerParser.Parse(response.Content, node.Answers!);
            if (parsed is not null)
                return (parsed, attempt, false, null, null);
            lastRawContent = response.Content;
            if (attempt == 1)
                conversation.AddMessage(new LlmMessage("user", "Return valid JSON using exactly one allowed answer."));
        }
        return (null, 2, false, lastRawContent, null);
    }

    private async Task EmitNodeVisitedAsync(
        string executionId,
        string nodeId,
        DecisionNode node,
        TimeSpan duration,
        string? outcome,
        string? nextNodeId,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var journalEvent = new DecisionNodeVisitedEvent(
            executionId,
            nodeId,
            node.Type,
            duration,
            outcome is null ? null : JsonSerializer.Serialize(outcome, AiClevernessJsonContext.Default.String),
            _defaultOptions?.TraceId,
            _defaultOptions?.CorrelationId,
            timestamp);
        await PublishAsync(
            journalEvent,
            new DecisionNodeVisitedBusEvent(
                executionId,
                nodeId,
                node.Type,
                duration,
                outcome,
                timestamp,
                _defaultOptions?.TraceId,
                _defaultOptions?.CorrelationId),
            cancellationToken);
        _transcript.Value?.AppendDecisionNode(nodeId, node, duration, outcome, nextNodeId);
    }

    private async Task EmitActionCompletedAsync(
        string executionId,
        string nodeId,
        string actionName,
        DecisionActionResult result,
        IReadOnlyList<DecisionData> producedData,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var journalEvent = new DecisionActionCompletedEvent(
            executionId,
            nodeId,
            actionName,
            result.Status,
            result.Error,
            _defaultOptions?.TraceId,
            _defaultOptions?.CorrelationId,
            timestamp);
        await PublishAsync(
            journalEvent,
            new DecisionActionCompletedBusEvent(
                executionId,
                nodeId,
                actionName,
                result.Status,
                result.Error,
                timestamp,
                _defaultOptions?.TraceId,
                _defaultOptions?.CorrelationId),
            cancellationToken);
        _transcript.Value?.AppendDecisionAction(nodeId, actionName, result, producedData);
    }

    private async Task EmitClassificationCompletedAsync(
        string executionId,
        string nodeId,
        EnumAnswer answer,
        int attempt,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var journalEvent = new DecisionClassificationCompletedEvent(
            executionId,
            nodeId,
            answer.Value,
            answer.Observation,
            answer.Confidence,
            attempt,
            _defaultOptions?.TraceId,
            _defaultOptions?.CorrelationId,
            timestamp);
        await PublishAsync(
            journalEvent,
            new DecisionClassificationCompletedBusEvent(
                executionId,
                nodeId,
                answer.Value,
                answer.Observation,
                answer.Confidence,
                attempt,
                timestamp,
                _defaultOptions?.TraceId,
                _defaultOptions?.CorrelationId),
            cancellationToken);
        _transcript.Value?.AppendDecisionClassification(
            nodeId,
            answer.Value,
            answer.Observation,
            answer.Confidence,
            attempt);
    }

    private async Task PublishAsync<TJournal, TBus>(
        TJournal journalEvent,
        TBus busEvent,
        CancellationToken cancellationToken)
        where TJournal : ExecutionEvent
        where TBus : IExecutionEvent
    {
        try
        {
            await _journal.AppendAsync(
                journalEvent.ExecutionId,
                journalEvent,
                SerializeJournalPayload(journalEvent),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Journaling is best effort for the execution path; callers can monitor failures in their journal implementation.
        }

        if (_eventPublisher is null)
            return;
        try
        {
            await _eventPublisher.PublishAsync(busEvent, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Event handlers are observational and cannot change the tree result.
        }
    }

    private static string? SerializeJournalPayload(ExecutionEvent journalEvent)
        => journalEvent switch
        {
            DecisionNodeVisitedEvent node => JsonSerializer.Serialize(
                node,
                AiClevernessJsonContext.Default.DecisionNodeVisitedEvent),
            DecisionActionCompletedEvent action => JsonSerializer.Serialize(
                action,
                AiClevernessJsonContext.Default.DecisionActionCompletedEvent),
            DecisionClassificationCompletedEvent classification => JsonSerializer.Serialize(
                classification,
                AiClevernessJsonContext.Default.DecisionClassificationCompletedEvent),
            _ => null
        };

    private TranscriptContext? CreateTranscript(
        DecisionTreeModel tree,
        string executionId)
    {
        if (string.IsNullOrWhiteSpace(_defaultOptions?.TranscriptDirectory))
            return null;

        var parameters = new Dictionary<string, object>
        {
            [AgentPropertyKeys.MarkdownTranscriptDirectory] = _defaultOptions.TranscriptDirectory!
        };
        if (_defaultOptions.TranscriptDebug)
            parameters[AgentPropertyKeys.MarkdownTranscriptDebug] = true;

        var request = new AgentRequest(
            $"Decision tree: {tree.TreeId}",
            Parameters: parameters);
        var runtimeOptions = new AgentRuntimeOptions
        {
            TranscriptRedactor = _defaultOptions.TranscriptRedactor
        };
        return TranscriptContext.Create(
            request,
            executionId,
            runtimeOptions,
            logger: null,
            decisionTranscriptPolicy: _defaultOptions.DecisionTranscriptPolicy);
    }

    private IConversationManager CreateConversationManager()
    {
        if (_conversationManagerFactory is not null)
            return _conversationManagerFactory.Create();
        if (_conversationManager is DefaultConversationManager defaultManager)
            return defaultManager.CreateForExecution();
        throw new InvalidOperationException(
            "A custom conversation manager requires an IConversationManagerFactory to guarantee execution isolation.");
    }

    private static DecisionNode GetNode(DecisionTreeModel tree, string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new InvalidOperationException("Decision tree node ID is missing.");
        if (tree.Nodes is null
            || !tree.Nodes.TryGetValue(nodeId, out var node)
            || node is null)
            throw new InvalidOperationException($"Decision tree node '{nodeId}' does not exist.");
        return node;
    }

    private IDecisionAction GetAction(string? actionName, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(actionName)
            || !_actions.TryGetValue(actionName, out var action))
            throw new InvalidOperationException(
                $"Action '{actionName ?? "<missing>"}' for node '{nodeId}' is not registered.");
        return action;
    }

    private IDecisionPredicate GetPredicate(string? predicateName, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(predicateName)
            || !_predicates.TryGetValue(predicateName, out var predicate))
            throw new InvalidOperationException(
                $"Predicate '{predicateName ?? "<missing>"}' for node '{nodeId}' is not registered.");
        return predicate;
    }

    private static DecisionTransition FindTransition(DecisionNode node, string condition)
    {
        if (node.Transitions is null)
            throw new InvalidOperationException("Decision tree node has no transitions.");

        var transition = node.Transitions.FirstOrDefault(candidate =>
            candidate is not null
            && string.Equals(candidate.Condition, condition, StringComparison.Ordinal));
        if (transition is null)
            throw new InvalidOperationException(
                $"Decision tree node has no transition for condition '{condition}'.");
        if (string.IsNullOrWhiteSpace(transition.NextNodeId))
            throw new InvalidOperationException(
                $"Decision tree transition for condition '{condition}' has no target node.");
        return transition;
    }

    private static void UpdateDuration(ResourceUsage usage, Stopwatch stopwatch)
        => usage.Duration = stopwatch.Elapsed;

    private static async Task<bool> LimitReachedAsync(
        int maximum,
        int current,
        ResourceLimitAction action,
        CancellationToken cancellationToken)
    {
        if (current < maximum)
            return false;
        if (action == ResourceLimitAction.Throttle)
            await Task.Delay(1, cancellationToken);
        return true;
    }

    private static async Task<bool> LimitExceededAsync(
        ResourceUsage usage,
        ResourceLimits limits,
        CancellationToken cancellationToken)
    {
        if (!usage.Exceeds(limits))
            return false;

        var hardCountLimitExceeded =
            (limits.MaxNodeVisits.HasValue && usage.NodeVisits > limits.MaxNodeVisits.Value)
            || (limits.MaxLlmCalls.HasValue && usage.LlmCalls > limits.MaxLlmCalls.Value);
        if (hardCountLimitExceeded)
        {
            if (limits.OnExceeded == ResourceLimitAction.Throttle)
                await Task.Delay(1, cancellationToken);
            return true;
        }

        if (limits.OnExceeded == ResourceLimitAction.Warn)
            return false;
        if (limits.OnExceeded == ResourceLimitAction.Throttle)
        {
            await Task.Delay(1, cancellationToken);
            return false;
        }
        return true;
    }

    private DecisionBudget ApplyDefaults(DecisionBudget budget)
    {
        if (_defaultOptions is null)
            return budget;
        return budget with
        {
            MaxNodeVisits = budget.HasMaxNodeVisits
                ? budget.MaxNodeVisits
                : _defaultOptions.DefaultMaxNodeVisits,
            MaxLlmCalls = budget.HasMaxLlmCalls
                ? budget.MaxLlmCalls
                : _defaultOptions.DefaultMaxLlmCalls,
            MaxElapsedTime = budget.HasMaxElapsedTime
                ? budget.MaxElapsedTime
                : _defaultOptions.DefaultMaxElapsedTime,
            MaxContextTokens = budget.HasMaxContextTokens
                ? budget.MaxContextTokens
                : _defaultOptions.DefaultMaxContextTokens
        };
    }

    private DecisionTreeResult CreateResult(
        string executionId,
        DecisionState state,
        DecisionTreeOutcome outcome,
        string? verdict,
        string? error)
    {
        var result = new DecisionTreeResult(
            executionId,
            outcome == DecisionTreeOutcome.Terminal,
            verdict,
            outcome,
            state.Classifications.ToArray(),
            state.ResourceUsage,
            error);
        _transcript.Value?.CompleteDecision(result);
        return result;
    }

    private static IReadOnlyDictionary<string, T> BuildCatalog<T>(IEnumerable<T> items)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        var catalog = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var name = item switch
            {
                IDecisionAction action => action.Name,
                IDecisionPredicate predicate => predicate.Name,
                _ => throw new InvalidOperationException("Unsupported decision catalog item.")
            };
            if (string.IsNullOrWhiteSpace(name) || !catalog.TryAdd(name, item))
                throw new InvalidOperationException($"Decision catalog contains duplicate or empty name '{name}'.");
        }
        return catalog;
    }

    private static string? TruncateForDisplay(string? content, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        var trimmed = content.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;
        return trimmed[..maxLength] + "...";
    }

}
