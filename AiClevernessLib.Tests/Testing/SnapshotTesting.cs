using System.Text.Json;

using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

/// <summary>
/// Snapshot testing utilities for <see cref="ExecutionSnapshot"/>.
/// Enables serializing, comparing, and verifying execution snapshots.
/// </summary>
public static class SnapshotTesting
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

    /// <summary>
    /// Creates a snapshot builder for constructing expected snapshots.
    /// </summary>
    public static SnapshotBuilder CreateSnapshot(string executionId, string goal)
    {
        return new SnapshotBuilder(executionId, goal);
    }

    /// <summary>
    /// Compares two snapshots and returns a list of differences.
    /// Returns an empty list if they are equivalent.
    /// </summary>
    public static IReadOnlyList<string> DiffSnapshots(
        this ExecutionSnapshot actual,
        ExecutionSnapshot expected)
    {
        var diffs = new List<string>();

        if (actual.ExecutionId != expected.ExecutionId)
            diffs.Add($"ExecutionId: '{actual.ExecutionId}' != '{expected.ExecutionId}'");

        if (actual.Status != expected.Status)
            diffs.Add($"Status: {actual.Status} != {expected.Status}");

        if (actual.Goal != expected.Goal)
            diffs.Add($"Goal: '{actual.Goal}' != '{expected.Goal}'");

        if (actual.ResultSuccess != expected.ResultSuccess)
            diffs.Add($"ResultSuccess: {actual.ResultSuccess} != {expected.ResultSuccess}");

        if (actual.ResultOutput != expected.ResultOutput)
            diffs.Add($"ResultOutput: '{actual.ResultOutput}' != '{expected.ResultOutput}'");

        if (actual.TurnCount != expected.TurnCount)
            diffs.Add($"TurnCount: {actual.TurnCount} != {expected.TurnCount}");

        if (actual.ToolInvocationCount != expected.ToolInvocationCount)
            diffs.Add(
                $"ToolInvocationCount: {actual.ToolInvocationCount} != {expected.ToolInvocationCount}");

        if (actual.QualityRetryCount != expected.QualityRetryCount)
            diffs.Add(
                $"QualityRetryCount: {actual.QualityRetryCount} != {expected.QualityRetryCount}");

        if (actual.ToolRetryCount != expected.ToolRetryCount)
            diffs.Add($"ToolRetryCount: {actual.ToolRetryCount} != {expected.ToolRetryCount}");

        return diffs;
    }

    /// <summary>
    /// Deserializes an execution snapshot from JSON.
    /// </summary>
    public static ExecutionSnapshot? FromSnapshotJson(string json)
    {
        return JsonSerializer.Deserialize<ExecutionSnapshot>(json, SnapshotJsonOptions);
    }

    /// <summary>
    /// Asserts that two snapshots are equivalent, throwing if they differ.
    /// </summary>
    /// <exception cref="SnapshotMismatchException">The snapshots differ.</exception>
    public static void ShouldMatchSnapshot(
        this ExecutionSnapshot actual,
        ExecutionSnapshot expected)
    {
        var diffs = actual.DiffSnapshots(expected);
        if (diffs.Count > 0)
        {
            throw new SnapshotMismatchException(
                $"Snapshot mismatch:\n{string.Join("\n", diffs)}");
        }
    }

    /// <summary>
    /// Serializes an execution snapshot to a stable JSON string for comparison.
    /// </summary>
    public static string ToSnapshotJson(this ExecutionSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }
}

/// <summary>
/// Fluent builder for constructing expected <see cref="ExecutionSnapshot"/> instances.
/// </summary>
public sealed class SnapshotBuilder
{
    private ExecutionSnapshot _snapshot;

    public SnapshotBuilder(string executionId, string goal)
    {
        _snapshot = new ExecutionSnapshot
                        {
                            ExecutionId = executionId,
                            Goal = goal,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
    }

    /// <summary>
    /// Builds the final snapshot for comparison.
    /// </summary>
    public ExecutionSnapshot Build() => _snapshot;

    public SnapshotBuilder WithQualityRetries(int count)
    {
        _snapshot = _snapshot with { QualityRetryCount = count };
        return this;
    }

    public SnapshotBuilder WithResult(bool success, string? output = null)
    {
        _snapshot = _snapshot with { ResultSuccess = success, ResultOutput = output };
        return this;
    }

    public SnapshotBuilder WithStatus(ExecutionStatus status)
    {
        _snapshot = _snapshot with { Status = status };
        return this;
    }

    public SnapshotBuilder WithToolInvocations(int count)
    {
        _snapshot = _snapshot with { ToolInvocationCount = count };
        return this;
    }

    public SnapshotBuilder WithToolRetries(int count)
    {
        _snapshot = _snapshot with { ToolRetryCount = count };
        return this;
    }

    public SnapshotBuilder WithTools(params string[] toolNames)
    {
        _snapshot = _snapshot with { AvailableToolNames = toolNames };
        return this;
    }

    public SnapshotBuilder WithTurnCount(int turns)
    {
        _snapshot = _snapshot with { TurnCount = turns };
        return this;
    }
}

/// <summary>
/// Exception thrown when snapshot comparison fails.
/// </summary>
public sealed class SnapshotMismatchException : Exception
{
    public SnapshotMismatchException(string message)
        : base(message)
    {
    }
}
