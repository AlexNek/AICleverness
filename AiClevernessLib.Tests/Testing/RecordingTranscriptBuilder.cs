using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using AiCleverness.Runtime.Transcript;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingTranscriptBuilder : ITranscriptBuilder
{
    private readonly MarkdownTranscriptBuilder _inner = new();
    private readonly object _gate = new();

    public List<string> ActionHeadings { get; } = [];

    public string Header(string goal, string executionId, DateTimeOffset startedAt, bool debug)
        => _inner.Header(goal, executionId, startedAt, debug);

    public string DecisionOverview(string treeId, int version, string startNodeId, string task)
        => _inner.DecisionOverview(treeId, version, startNodeId, task);

    public string DebugRequest(IReadOnlyDictionary<string, object> parameters)
        => _inner.DebugRequest(parameters);

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
        => _inner.DebugRuntime(
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

    public string Turn(int turn, int qualityAttempt, int failoverAttempt, string? model)
        => _inner.Turn(turn, qualityAttempt, failoverAttempt, model);

    public string ModelContent(string content)
        => _inner.ModelContent(content);

    public string ToolDecision(string model, string toolName, string? callId, string arguments)
        => _inner.ToolDecision(model, toolName, callId, arguments);

    public string ToolResult(
        string toolName,
        string? callId,
        string status,
        string? output,
        string? error)
        => _inner.ToolResult(toolName, callId, status, output, error);

    public string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData)
    {
        lock (_gate)
        {
            ActionHeadings.Add(nodeName ?? actionKey);
        }

        return _inner.DecisionAction(
            nodeId,
            actionKey,
            nodeName,
            status,
            outcomeSummary,
            error,
            producedData);
    }

    public string DecisionClassification(
        string nodeId,
        string answer,
        string? observation,
        string? confidence,
        int attempt)
        => _inner.DecisionClassification(nodeId, answer, observation, confidence, attempt);

    public string DecisionLlmAttempt(
        string nodeId,
        int attempt,
        IReadOnlyList<LlmMessage> messages,
        string? response,
        string? finishReason,
        LlmTokenUsage? usage)
        => _inner.DecisionLlmAttempt(nodeId, attempt, messages, response, finishReason, usage);

    public string DecisionResult(
        DecisionTreeOutcome outcome,
        bool succeeded,
        string? verdict,
        string? error,
        ResourceUsage usage,
        IReadOnlyList<string> path,
        int omittedSectionCount = 0,
        IReadOnlyList<KeyValuePair<string, string>>? stateProperties = null)
        => _inner.DecisionResult(
            outcome,
            succeeded,
            verdict,
            error,
            usage,
            path,
            omittedSectionCount,
            stateProperties);

    public string Retry(string reason, int retryNumber)
        => _inner.Retry(reason, retryNumber);

    public string Status(string status, string? detail)
        => _inner.Status(status, detail);

    public string Final(AgentResult result, string status)
        => _inner.Final(result, status);

    public string FinalFailure(string status, string detail)
        => _inner.FinalFailure(status, detail);
}
