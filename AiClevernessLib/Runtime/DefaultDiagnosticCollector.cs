using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory <see cref="IDiagnosticCollector"/> that stores diagnostic entries per execution.
/// </summary>
public sealed class DefaultDiagnosticCollector : IDiagnosticCollector
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<DiagnosticEntry>> _entries = new();

    /// <inheritdoc />
    public Task ClearAsync(string executionId, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(executionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DiagnosticEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DiagnosticEntry> all = _entries.Values
            .SelectMany(b => b)
            .OrderBy(e => e.Timestamp)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(all);
    }

    /// <inheritdoc />
    public Task<DiagnosticReport> GetReportAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.TryGetValue(executionId, out var bag)
                          ? bag.OrderBy(e => e.Timestamp).ToList().AsReadOnly()
                          : (IReadOnlyList<DiagnosticEntry>)Array.Empty<DiagnosticEntry>();

        var report = new DiagnosticReport { ExecutionId = executionId, Entries = entries };

        return Task.FromResult(report);
    }

    /// <inheritdoc />
    public Task RecordAsync(DiagnosticEntry entry, CancellationToken cancellationToken = default)
    {
        var bag = _entries.GetOrAdd(entry.ExecutionId, _ => new ConcurrentBag<DiagnosticEntry>());
        bag.Add(entry);
        return Task.CompletedTask;
    }
}
