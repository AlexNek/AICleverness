using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory <see cref="IMetricsCollector"/> that aggregates execution metrics.
/// </summary>
public sealed class DefaultMetricsCollector : IMetricsCollector
{
    private const double MedianPercentile = 0.50;
    private const double P95Percentile = 0.95;
    private const double P99Percentile = 0.99;

    private readonly object _lock = new();

    private readonly ConcurrentDictionary<string, ExecutionManifest> _manifests = new();

    /// <inheritdoc />
    public Task<ExecutionMetrics> GetAggregateMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var manifests = _manifests.Values.ToList();
        var metrics = ComputeAggregate(manifests);
        return Task.FromResult(metrics);
    }

    /// <inheritdoc />
    public Task<ExecutionMetrics?> GetExecutionMetricsAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_manifests.TryGetValue(executionId, out var manifest))
            return Task.FromResult<ExecutionMetrics?>(null);

        var metrics = ComputeSingle(manifest);
        return Task.FromResult<ExecutionMetrics?>(metrics);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolMetrics>> GetToolMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var toolDurations =
            new ConcurrentDictionary<string, (long Count, long Failures, double TotalMs, double
                MaxMs)>();

        foreach (var manifest in _manifests.Values)
        {
            foreach (var evt in manifest.Events)
            {
                if (evt is ToolCompletedEvent tce)
                {
                    var ms = tce.Duration.TotalMilliseconds;
                    toolDurations.AddOrUpdate(
                        tce.ToolName,
                        _ => (1, tce.Result.Success ? 0 : 1, ms, ms),
                        (_, existing) => (
                                             existing.Count + 1,
                                             existing.Failures + (tce.Result.Success ? 0 : 1),
                                             existing.TotalMs + ms,
                                             Math.Max(existing.MaxMs, ms)));
                }
            }
        }

        IReadOnlyList<ToolMetrics> result = toolDurations
            .Select(kvp => new ToolMetrics(
                kvp.Key,
                kvp.Value.Count,
                kvp.Value.Failures,
                TimeSpan.FromMilliseconds(
                    kvp.Value.Count > 0 ? kvp.Value.TotalMs / kvp.Value.Count : 0),
                TimeSpan.FromMilliseconds(kvp.Value.MaxMs)))
            .ToList()
            .AsReadOnly();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task RecordAsync(
        ExecutionManifest manifest,
        CancellationToken cancellationToken = default)
    {
        _manifests[manifest.ExecutionId] = manifest;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _manifests.Clear();
        return Task.CompletedTask;
    }

    private static ExecutionMetrics ComputeAggregate(IReadOnlyList<ExecutionManifest> manifests)
    {
        if (manifests.Count == 0)
            return new ExecutionMetrics();

        var durations = manifests
            .Where(m => m.Duration.HasValue)
            .Select(m => m.Duration!.Value)
            .OrderBy(d => d)
            .ToList();

        var llmDurations = GetLlmDurations(manifests.SelectMany(m => m.Events));
        var toolEvents = manifests.SelectMany(m => m.Events).OfType<ToolCompletedEvent>().ToList();
        var qualityEvents = manifests.SelectMany(m => m.Events).OfType<QualityGateRejectedEvent>()
            .ToList();

        var totalToolInvocations = manifests.Sum(m =>
            m.Events.Count(e => e is ToolInvokedEvent));

        return new ExecutionMetrics
                   {
                       TotalExecutions = manifests.Count,
                       SuccessfulExecutions =
                           manifests.Count(m => m.Status == ExecutionStatus.Completed),
                       FailedExecutions = manifests.Count(m => m.Status == ExecutionStatus.Failed),
                       BlockedExecutions =
                           manifests.Count(m => m.Status == ExecutionStatus.Blocked),
                       TimedOutExecutions =
                           manifests.Count(m => m.Status == ExecutionStatus.TimedOut),
                       CancelledExecutions =
                           manifests.Count(m => m.Status == ExecutionStatus.Cancelled),
                       AverageDuration =
                           durations.Count > 0
                               ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks))
                               : null,
                       MinDuration = durations.Count > 0 ? durations.First() : null,
                       MaxDuration = durations.Count > 0 ? durations.Last() : null,
                       P50Duration = GetPercentile(durations, MedianPercentile),
                       P95Duration = GetPercentile(durations, P95Percentile),
                       P99Duration = GetPercentile(durations, P99Percentile),
                       TotalLlmCalls = llmDurations.Count,
                       AverageLlmDuration =
                           llmDurations.Count > 0
                               ? TimeSpan.FromTicks((long)llmDurations.Average(d => d.Ticks))
                               : null,
                       TotalToolInvocations = totalToolInvocations,
                       FailedToolInvocations = toolEvents.Count(e => !e.Result.Success),
                       AverageToolDuration =
                           toolEvents.Count > 0
                               ? TimeSpan.FromTicks((long)toolEvents.Average(e => e.Duration.Ticks))
                               : null,
                       TotalQualityGateEvaluations = qualityEvents.Count,
                       QualityGateRejections = qualityEvents.Count,
                       TotalQualityRetries = qualityEvents.Sum(e => e.RetryCount),
                       TotalToolRetries = manifests.Sum(m => m.ToolRetryCount)
                   };
    }

    private static ExecutionMetrics ComputeSingle(ExecutionManifest manifest)
    {
        var llmDurations = GetLlmDurations(manifest.Events);
        var toolEvents = manifest.Events.OfType<ToolCompletedEvent>().ToList();
        var qualityEvents = manifest.Events.OfType<QualityGateRejectedEvent>().ToList();

        return new ExecutionMetrics
                   {
                       ExecutionId = manifest.ExecutionId,
                       TotalExecutions = 1,
                       SuccessfulExecutions = manifest.Status == ExecutionStatus.Completed ? 1 : 0,
                       FailedExecutions = manifest.Status == ExecutionStatus.Failed ? 1 : 0,
                       BlockedExecutions = manifest.Status == ExecutionStatus.Blocked ? 1 : 0,
                       TimedOutExecutions = manifest.Status == ExecutionStatus.TimedOut ? 1 : 0,
                       CancelledExecutions = manifest.Status == ExecutionStatus.Cancelled ? 1 : 0,
                       AverageDuration = manifest.Duration,
                       MinDuration = manifest.Duration,
                       MaxDuration = manifest.Duration,
                       TotalLlmCalls = llmDurations.Count,
                       AverageLlmDuration =
                           llmDurations.Count > 0
                               ? TimeSpan.FromTicks((long)llmDurations.Average(d => d.Ticks))
                               : null,
                       TotalToolInvocations = manifest.Events.Count(e => e is ToolInvokedEvent),
                       FailedToolInvocations = toolEvents.Count(e => !e.Result.Success),
                       AverageToolDuration =
                           toolEvents.Count > 0
                               ? TimeSpan.FromTicks((long)toolEvents.Average(e => e.Duration.Ticks))
                               : null,
                       TotalQualityGateEvaluations = qualityEvents.Count,
                       QualityGateRejections = qualityEvents.Count,
                       TotalQualityRetries = qualityEvents.Sum(e => e.RetryCount),
                       TotalToolRetries = manifest.ToolRetryCount
                   };
    }

    private static TimeSpan? GetPercentile(List<TimeSpan> sorted, double percentile)
    {
        if (sorted.Count == 0) return null;
        var index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    /// <summary>
    /// Durations of every LLM attempt — successful responses and failed
    /// attempts alike — so attempt metrics never drop failures.
    /// </summary>
    private static List<TimeSpan> GetLlmDurations(IEnumerable<ExecutionEvent> events)
    {
        var durations = new List<TimeSpan>();
        foreach (var evt in events)
        {
            if (evt is LlmRespondedEvent responded)
                durations.Add(responded.Duration);
            else if (evt is LlmFailedEvent failed)
                durations.Add(failed.Duration);
        }

        return durations;
    }
}
