using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Renders individual transcript sections for one execution.
/// Implementations must return non-null content and should not retain state across executions.
/// </summary>
public interface ITranscriptBuilder
{
    string Header(string goal, string executionId, DateTimeOffset startedAt, bool debug);

    string DecisionOverview(string treeId, int version, string startNodeId, string task);

    string DebugRequest(IReadOnlyDictionary<string, object> parameters);

    string DebugRuntime(
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
        string? model);

    string Turn(int turn, int qualityAttempt, int failoverAttempt, string? model);

    string ModelContent(string content);

    string ToolDecision(string model, string toolName, string? callId, string arguments);

    string ToolResult(
        string toolName,
        string? callId,
        string status,
        string? output,
        string? error);

    string DecisionAction(
        string nodeId,
        string actionKey,
        string? nodeName,
        DecisionActionStatus status,
        string? outcomeSummary,
        string? error,
        string? producedData);

    string DecisionClassification(
        string nodeId,
        string answer,
        string? observation,
        string? confidence,
        int attempt);

    string DecisionLlmAttempt(
        string nodeId,
        int attempt,
        IReadOnlyList<LlmMessage> messages,
        string? response,
        string? finishReason,
        LlmTokenUsage? usage);

    string DecisionResult(
        DecisionTreeOutcome outcome,
        bool succeeded,
        string? verdict,
        string? error,
        ResourceUsage usage,
        IReadOnlyList<string> path,
        int omittedSectionCount = 0,
        IReadOnlyList<KeyValuePair<string, string>>? stateProperties = null);

    string Retry(string reason, int retryNumber);

    string Status(string status, string? detail);

    string Final(AgentResult result, string status);

    string FinalFailure(string status, string detail);
}
