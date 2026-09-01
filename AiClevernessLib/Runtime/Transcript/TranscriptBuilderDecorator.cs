using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Delegates transcript rendering to another builder while allowing applications
/// to override only the sections they need to customize.
/// </summary>
/// <remarks>
/// The parameterless constructor wraps a new <see cref="MarkdownTranscriptBuilder"/>.
/// Create a fresh decorator for each execution through
/// <c>TranscriptBuilderFactory</c>; do not share mutable builder state between executions.
/// </remarks>
public class TranscriptBuilderDecorator : ITranscriptBuilder
{
    /// <summary>Initializes a decorator using the default Markdown builder.</summary>
    public TranscriptBuilderDecorator()
        : this(new MarkdownTranscriptBuilder())
    {
    }

    /// <summary>Initializes a decorator around the supplied builder.</summary>
    /// <param name="inner">Builder that receives calls not overridden by the decorator.</param>
    public TranscriptBuilderDecorator(ITranscriptBuilder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    /// <summary>Gets the builder used for delegated sections.</summary>
    protected ITranscriptBuilder Inner { get; }

    /// <inheritdoc />
    public virtual string Header(string goal, string executionId, DateTimeOffset startedAt, bool debug)
        => Inner.Header(goal, executionId, startedAt, debug);

    /// <inheritdoc />
    public virtual string DecisionOverview(string treeId, int version, string startNodeId, string task)
        => Inner.DecisionOverview(treeId, version, startNodeId, task);

    /// <inheritdoc />
    public virtual string DebugRequest(IReadOnlyDictionary<string, object> parameters)
        => Inner.DebugRequest(parameters);

    /// <inheritdoc />
    public virtual string DebugRuntime(
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
        => Inner.DebugRuntime(
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
            model);

    /// <inheritdoc />
    public virtual string Turn(int turn, int qualityAttempt, int failoverAttempt, string? model)
        => Inner.Turn(turn, qualityAttempt, failoverAttempt, model);

    /// <inheritdoc />
    public virtual string ModelContent(string content)
        => Inner.ModelContent(content);

    /// <inheritdoc />
    public virtual string ToolDecision(string model, string toolName, string? callId, string arguments)
        => Inner.ToolDecision(model, toolName, callId, arguments);

    /// <inheritdoc />
    public virtual string ToolResult(
        string toolName,
        string? callId,
        string status,
        string? output,
        string? error)
        => Inner.ToolResult(toolName, callId, status, output, error);

    /// <inheritdoc />
    public virtual string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData)
        => Inner.DecisionAction(
            nodeId,
            actionKey,
            nodeName,
            status,
            outcomeSummary,
            error,
            producedData);

    /// <inheritdoc />
    public virtual string DecisionClassification(
        string nodeId,
        string answer,
        string? observation,
        string? confidence,
        int attempt)
        => Inner.DecisionClassification(nodeId, answer, observation, confidence, attempt);

    /// <inheritdoc />
    public virtual string DecisionLlmAttempt(
        string nodeId,
        int attempt,
        IReadOnlyList<LlmMessage> messages,
        string? response,
        string? finishReason,
        LlmTokenUsage? usage)
        => Inner.DecisionLlmAttempt(
            nodeId,
            attempt,
            messages,
            response,
            finishReason,
            usage);

    /// <inheritdoc />
    public virtual string DecisionResult(
        DecisionTreeOutcome outcome,
        bool succeeded,
        string? verdict,
        string? error,
        ResourceUsage usage,
        IReadOnlyList<string> path,
        int omittedSectionCount = 0,
        IReadOnlyList<KeyValuePair<string, string>>? stateProperties = null)
        => Inner.DecisionResult(
            outcome,
            succeeded,
            verdict,
            error,
            usage,
            path,
            omittedSectionCount,
            stateProperties);

    /// <inheritdoc />
    public virtual string Retry(string reason, int retryNumber)
        => Inner.Retry(reason, retryNumber);

    /// <inheritdoc />
    public virtual string Status(string status, string? detail)
        => Inner.Status(status, detail);

    /// <inheritdoc />
    public virtual string Final(AgentResult result, string status)
        => Inner.Final(result, status);

    /// <inheritdoc />
    public virtual string FinalFailure(string status, string detail)
        => Inner.FinalFailure(status, detail);
}
