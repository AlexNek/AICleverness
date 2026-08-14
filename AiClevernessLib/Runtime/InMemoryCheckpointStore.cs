using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory <see cref="ICheckpointStore"/> suitable for tests, local development,
/// and single-process scenarios where persistence across restarts is not required.
/// </summary>
public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly
        ConcurrentDictionary<string, List<(CheckpointEntry Entry, ExecutionSnapshot Snapshot)>>
        _store = new();

    /// <inheritdoc />
    public Task DeleteAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        _store.TryRemove(executionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CheckpointEntry>> ListAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(executionId, out var bucket))
            return Task.FromResult<IReadOnlyList<CheckpointEntry>>(Array.Empty<CheckpointEntry>());

        lock (bucket)
        {
            IReadOnlyList<CheckpointEntry> entries = bucket
                .OrderByDescending(x => x.Entry.CapturedAt)
                .Select(x => x.Entry)
                .ToList()
                .AsReadOnly();
            return Task.FromResult(entries);
        }
    }

    /// <inheritdoc />
    public Task<ExecutionSnapshot?> LoadAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        foreach (var bucket in _store.Values)
        {
            lock (bucket)
            {
                var match = bucket.FirstOrDefault(x => x.Entry.CheckpointId == checkpointId);
                if (match.Snapshot is not null)
                    return Task.FromResult<ExecutionSnapshot?>(match.Snapshot);
            }
        }

        return Task.FromResult<ExecutionSnapshot?>(null);
    }

    /// <inheritdoc />
    public Task<ExecutionSnapshot?> LoadLatestAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(executionId, out var bucket))
            return Task.FromResult<ExecutionSnapshot?>(null);

        lock (bucket)
        {
            var latest = bucket
                .OrderByDescending(x => x.Entry.CapturedAt)
                .FirstOrDefault();
            return Task.FromResult<ExecutionSnapshot?>(latest.Snapshot);
        }
    }

    /// <inheritdoc />
    public Task<CheckpointEntry> SaveAsync(
        ExecutionSnapshot snapshot,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        var entry = CheckpointEntry.From(snapshot, label);
        var bucket = _store.GetOrAdd(
            snapshot.ExecutionId,
            _ => new List<(CheckpointEntry, ExecutionSnapshot)>());

        lock (bucket)
        {
            bucket.Add((entry, snapshot));
        }

        return Task.FromResult(entry);
    }
}
